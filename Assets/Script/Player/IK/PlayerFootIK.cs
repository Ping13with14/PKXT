using UnityEngine;

/// <summary>
/// 脚步 IK（基于 Unity 原生 OnAnimatorIK）：
/// 待机/慢跑/快跑等地面状态时，双脚从脚踝向下检测地面，把脚踝吸附到地面
/// （含斜坡法线贴合、障碍顶面踩踏），解决脚悬空/陷地/滑动问题。
///
/// 注意：OnAnimatorIK 只在与 Animator 同一 GameObject 上的组件回调，
/// 本组件必须挂在 Player 根物体（Animator 所在物体），不能挂在 Model 子物体。
/// 跳跃/翻越时权重自动平滑退出，不干预动画与 MatchTarget。
///
/// 抗"吸地"设计：
/// - maxLiftCorrection 限制只修正离地面很近的脚，摆动脚（离地较高）完全跟随动画；
/// - heightSpeed 让脚踝高度向目标平滑过渡（MoveTowards），不做瞬移吸附；
/// - weightSpeed 控制 IK 权重渐入渐出，避免突然生效。
/// </summary>
public class PlayerFootIK : MonoBehaviour
{
    [Header("地面检测")]
    [Tooltip("脚部检测层级；值为 0 时运行时自动取 PlayerController.GroundLayer")]
    public LayerMask groundLayer;
    [Tooltip("从脚踝向上偏移的距离后再向下发射检测射线，支持检测高于动画脚位的地面（上坡）")]
    public float rayUpOffset = 0.15f;
    [Tooltip("向下检测总长度（含 rayUpOffset）")]
    public float rayDistance = 0.6f;
    [Tooltip("脚踝目标点相对地面的抬高量（脚踝到脚底的高度差，按模型微调）")]
    public float footGroundOffset = 0.1f;
    [Tooltip("动画脚位高出地面超过该值时视为摆动脚，不做吸附（避免空中脚被拉向地面；越小越不易'吸地'）")]
    public float maxLiftCorrection = 0.1f;

    [Header("平滑")]
    [Tooltip("IK 权重插值速度（每秒），越大生效越快；越小越柔和")]
    public float weightSpeed = 4f;
    [Tooltip("脚踝高度向目标平滑过渡的速度（米/秒），避免瞬移吸附")]
    public float heightSpeed = 6f;

    [Header("调试")]
    [Tooltip("场景视图中绘制检测射线与目标点")]
    public bool debugDraw = true;

    private Animator _animator;
    private PlayerController _controller;
    private LayerMask _resolvedGroundLayer;

    // 左右脚当前 IK 权重、已应用高度与最近一次检测结果
    private float _leftWeight;
    private float _rightWeight;
    private float _leftAppliedY;
    private float _rightAppliedY;
    private Vector3 _leftTargetPos;
    private Vector3 _rightTargetPos;
    private Quaternion _leftTargetRot;
    private Quaternion _rightTargetRot;
    private bool _leftHit;
    private bool _rightHit;
    private RaycastHit _leftHitInfo;
    private RaycastHit _rightHitInfo;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null || !_animator.isHuman)
        {
            Debug.LogWarning("PlayerFootIK 需要 Humanoid Animator（挂在 Player 根物体上）才能生效");
            enabled = false;
            return;
        }
        ResolveLayerMask();
    }

    private void Update()
    {
        ResolveLayerMask();
    }

    /// <summary>
    /// 解析检测层级：未手动指定时自动跟随 PlayerController.GroundLayer
    /// </summary>
    private void ResolveLayerMask()
    {
        if (groundLayer.value != 0)
        {
            _resolvedGroundLayer = groundLayer;
            return;
        }
        if (_controller == null)
            _controller = PlayerController.INSTANCE;
        if (_controller != null && _controller.GroundLayer.value != 0)
            _resolvedGroundLayer = _controller.GroundLayer;
    }

    // 翻越/攀爬状态在动作临近结束时置 true：提前渐入脚步 IK，
    // 让脚在动作结束前就贴合顶面/地面，避免结束后脚部没入
    private bool _forceFeetIK;
    // 攀爬收尾的"贴线"目标：把脚水平贴到前缘向内 climbTopInset 的停靠线平面
    private bool _snapToInset;
    private Vector3 _snapOrigin;
    private Vector3 _snapNormal;

    /// <summary>
    /// 强制启用/关闭脚步 IK（由 PlayerClimbObstacleState 在动作临近结束时调用）。
    /// 强制启用时忽略状态/地面/kinematic 检查；射线未命中或脚离表面过远时仍会自动跳过，
    /// 不会把空中的脚拉向地面。
    /// </summary>
    public void AllowFeetIK(bool allow) => _forceFeetIK = allow;

    /// <summary>
    /// 设置攀爬收尾的脚部贴线目标：把脚水平贴到过 origin、法线为 normal 的平面
    /// （即"前缘向内 climbTopInset"的停靠线，仅对齐纵深、保留左右间距）。
    /// </summary>
    public void SetFootSnapTarget(Vector3 origin, Vector3 normal)
    {
        _snapToInset = true;
        _snapOrigin = origin;
        _snapNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.forward;
    }

    /// <summary>
    /// 清除脚部贴线目标（状态退出时调用）
    /// </summary>
    public void ClearFootSnapTarget() => _snapToInset = false;

    /// <summary>
    /// 是否启用脚步 IK：默认仅地面状态（待机/慢跑/快跑）+ 在地面上 + 非翻越（刚体非 kinematic）；
    /// 翻越/攀爬临近结束时由状态强制启用（_forceFeetIK）
    /// </summary>
    private bool ShouldApplyIK()
    {
        if (_forceFeetIK)
            return true;
        if (_controller == null)
            return false;
        if (_controller.playerModel == null)
            return false;

        // 翻越/攀爬时肢体由 MatchTarget 接管，脚步 IK 必须退出
        if (_controller.playerModel.state == PlayerState.ClimbObstacle)
            return false;
        // 空中（跳跃/掉落）不吸附
        if (!_controller.isGround)
            return false;
        // 防御：翻越期间刚体为 kinematic，双保险退出
        if (_controller.playerRigidbody != null && _controller.playerRigidbody.isKinematic)
            return false;
        return true;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (_animator == null || !_animator.isHuman)
            return;

        bool apply = ShouldApplyIK();
        ApplyFootIK(AvatarIKGoal.LeftFoot, apply, ref _leftWeight, ref _leftAppliedY,
            ref _leftTargetPos, ref _leftTargetRot, ref _leftHit, ref _leftHitInfo);
        ApplyFootIK(AvatarIKGoal.RightFoot, apply, ref _rightWeight, ref _rightAppliedY,
            ref _rightTargetPos, ref _rightTargetRot, ref _rightHit, ref _rightHitInfo);

        if (debugDraw)
            DrawDebug(apply);
    }

    /// <summary>
    /// 单脚 IK：权重平滑过渡；命中地面且脚离地不高时把脚踝平滑吸附到地面并贴合斜坡法线。
    /// 摆动脚（离地过高/未命中）完全跟随动画，只调权重不调位置。
    /// </summary>
    private void ApplyFootIK(AvatarIKGoal goal, bool apply,
        ref float weight, ref float appliedY,
        ref Vector3 targetPos, ref Quaternion targetRot,
        ref bool hit, ref RaycastHit hitInfo)
    {
        float targetWeight = apply ? 1f : 0f;
        weight = Mathf.MoveTowards(weight, targetWeight, weightSpeed * Time.deltaTime);

        _animator.SetIKPositionWeight(goal, weight);
        _animator.SetIKRotationWeight(goal, weight);
        if (weight <= 0.0001f)
        {
            hit = false;
            return;
        }

        // 脚踝当前位置（动画驱动，未施加本次 IK 修正）
        Vector3 footPos = _animator.GetIKPosition(goal);
        // 从脚踝上方向下检测地面：起点抬高可命中高于动画脚位的地面（上坡/台阶）
        Vector3 origin = footPos + Vector3.up * rayUpOffset;
        hit = Physics.Raycast(origin, Vector3.down, out hitInfo,
            rayUpOffset + rayDistance, _resolvedGroundLayer, QueryTriggerInteraction.Ignore);

        if (!hit)
        {
            // 未命中：完全跟随动画
            appliedY = footPos.y;
            return;
        }

        // 摆动脚保护：动画脚位离地面太远（空中摆腿）时不吸附，避免脚被拉到地面。
        // 强制阶段（攀爬收尾）阈值更严：只防脚下沉（没入），不把姿态里抬起的脚拽到表面，
        // 避免"左脚先吸附检测点一会再恢复"的顿挫
        float liftLimit = _forceFeetIK ? 0.02f : maxLiftCorrection;
        float lift = footPos.y - (hitInfo.point.y + footGroundOffset);
        if (lift > liftLimit)
        {
            appliedY = footPos.y;
            return;
        }

        // 高度向目标平滑过渡（非瞬移），避免"还没落地就被吸住"的顿挫感
        float targetY = hitInfo.point.y + footGroundOffset;
        appliedY = Mathf.MoveTowards(appliedY, targetY, heightSpeed * Time.deltaTime);

        targetPos = footPos;
        targetPos.y = appliedY;
        // 攀爬收尾：水平方向把脚贴到"前缘向内 climbTopInset"的停靠线平面上
        // （只对齐纵深，保留左右间距；由 PlayerClimbObstacleState 在动作临近结束时设置）
        if (_forceFeetIK && _snapToInset)
            targetPos -= _snapNormal * Vector3.Dot(targetPos - _snapOrigin, _snapNormal);
        // 斜坡贴合：脚旋转到地面法线方向
        targetRot = Quaternion.FromToRotation(Vector3.up, hitInfo.normal)
                    * _animator.GetIKRotation(goal);

        _animator.SetIKPosition(goal, targetPos);
        _animator.SetIKRotation(goal, targetRot);
    }

    private void DrawDebug(bool apply)
    {
        if (_resolvedGroundLayer.value == 0)
            return;

        DrawFootDebug(AvatarIKGoal.LeftFoot, apply, _leftHit, _leftHitInfo);
        DrawFootDebug(AvatarIKGoal.RightFoot, apply, _rightHit, _rightHitInfo);
    }

    private void DrawFootDebug(AvatarIKGoal goal, bool apply, bool hit, RaycastHit hitInfo)
    {
        Vector3 footPos = _animator.GetIKPosition(goal);
        Vector3 origin = footPos + Vector3.up * rayUpOffset;
        Color rayColor = apply ? (hit ? Color.green : Color.yellow) : Color.gray;
        Debug.DrawLine(origin, origin + Vector3.down * (rayUpOffset + rayDistance), rayColor);

        if (hit)
        {
            Debug.DrawLine(origin, hitInfo.point, Color.red);
            // 目标点用十字线表示（脚踝应到达的位置）
            Vector3 target = hitInfo.point + Vector3.up * footGroundOffset;
            float s = 0.03f;
            Debug.DrawLine(target + Vector3.left * s, target + Vector3.right * s, Color.cyan);
            Debug.DrawLine(target + Vector3.down * s, target + Vector3.up * s, Color.cyan);
        }
    }
}
