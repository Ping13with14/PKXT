using System;
using UnityEngine;

/// <summary>
/// 根运动驱动：把动画根运动显式应用到根物体（挂在 Player 根，与 Animator 同物体，
/// OnAnimatorMove 才会被 Unity 回调；挂在子物体上的同名回调不会被调用）。
///
/// 需要本组件的原因（勿回退）：
/// 1. Animator 挂在从不旋转的 Player 根物体上，角色朝向在会旋转的 Model 子物体上，
///    因此 animator.deltaPosition 是"按 Animator 自身朝向（世界 +Z）"的增量，
///    必须旋转到模型朝向再应用——否则朝 -Z 翻越时会被推向 +Z（与 MatchTarget 打架产生抽搐）。
/// 2. 翻越期间刚体为 kinematic，Unity 内置根运动应用对 kinematic 刚体不可靠，
///    这里用 Rigidbody.MovePosition 显式驱动。
///
/// 只应用位移、不应用旋转：攀爬/翻越剪辑带根运动旋转曲线，逐帧累积在根物体上会造成
/// 疯狂旋转；骨骼自身的旋转/扭转由动画骨曲线表现。角色朝向在进入翻越时由
/// PlayerClimbObstacleState 锁定到障碍物表面法线反方向，翻越期间保持不变。
///
/// 高度适配：翻越/攀爬时有两种覆盖模式（由 PlayerClimbObstacleState 设置）：
/// - overrideY：只丢弃根运动 Y，XZ 仍由根运动驱动，Y 按动画进度由 yTargetCurve 驱动（翻越用）；
/// - overridePosition：完全丢弃根运动位移，位置按动画进度由 positionCurve 直接驱动
///   （攀爬两段式防穿模用：先贴墙上升到顶、过顶后再前移到顶面）。
///
/// 仅在 kinematic（翻越/攀爬）时生效；平时 applyRootMotion=false，本回调不会被调用。
/// </summary>
public class PlayerRootMotionDriver : MonoBehaviour
{
    [Header("高度适配（由翻越状态驱动）")]
    [Tooltip("翻越时根位置 Y 峰值相对障碍顶面的余量")]
    public float vaultClearance = 0.2f;
    [Tooltip("攀爬站上顶后根位置 Y 相对障碍顶面的偏移（略高于顶面，配合重力落定，防脚没入）")]
    public float climbStandOffset = 0.04f;
    [Tooltip("攀爬上升阶段根位置相对障碍前表面的退避距离（胶囊半径+间隙，避免贴墙过近）")]
    public float climbStandoff = 0.2f;
    [Tooltip("攀爬两段式轨迹的分界进度：此前贴墙上升，此后前移到顶面")]
    [Range(0f, 1f)] public float climbOverProgress = 0.5f;
    [Tooltip("攀爬站上顶后的停止位置：前表面向里的距离（越小越靠前缘）")]
    public float climbTopInset = 0.2f;

    private Animator _animator;
    private Rigidbody _rigidbody;
    private PlayerController _controller;
    private PlayerModel _model;

    // 可选：Y 轴由外部目标曲线驱动（参数=动画归一化进度 0~1，返回目标根 Y）。
    // 由 PlayerClimbObstacleState 在进入翻越时设置、退出时清除
    public bool overrideY;
    public Func<float, float> yTargetCurve;

    // 可选：全位置由外部目标曲线驱动（参数=动画归一化进度 0~1，返回目标世界位置）。
    // 攀爬两段式轨迹用；启用后忽略根运动位移与 overrideY
    public bool overridePosition;
    public Func<float, Vector3> positionCurve;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void LateUpdate()
    {
        // 延迟解析引用：PlayerController.Awake 在本组件 Awake 之后执行
        if (_controller == null)
        {
            _controller = PlayerController.INSTANCE;
            if (_controller != null)
                _model = _controller.playerModel;
        }
    }

    private void OnAnimatorMove()
    {
        if (_animator == null || _rigidbody == null)
            return;

        // 仅 kinematic（翻越/攀爬）期间手动应用根运动；平时不处理
        if (!_rigidbody.isKinematic)
            return;

        // 全位置轨迹覆盖（攀爬两段式）：位置完全由外部曲线决定
        if (overridePosition && positionCurve != null)
        {
            float progress = Mathf.Clamp01(_animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
            _rigidbody.MovePosition(positionCurve(progress));
            return;
        }

        // 动画根运动位移增量（世界空间，按 Animator 自身朝向计算）
        Vector3 delta = _animator.deltaPosition;

        // 旋转到模型朝向：Animator 在 Player 根（从不旋转），朝向在 Model 子物体上，
        // 若不做偏航修正，朝 -Z 翻越时根运动会把角色推向 +Z
        if (_model != null)
        {
            float yawOffset = _model.transform.eulerAngles.y - _animator.transform.eulerAngles.y;
            delta = Quaternion.Euler(0f, yawOffset, 0f) * delta;
        }

        // 高度适配：丢弃根运动 Y，改为按动画进度跟随外部目标高度曲线
        if (overrideY && yTargetCurve != null)
        {
            float progress = Mathf.Clamp01(_animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
            delta.y = yTargetCurve(progress) - _rigidbody.position.y;
        }

        // 只应用位移。不应用 deltaRotation：根运动旋转曲线会累积成疯狂旋转，
        // 朝向已锁定（PlayerClimbObstacleState.LockFacingToObstacle）
        _rigidbody.MovePosition(_rigidbody.position + delta);
    }
}
