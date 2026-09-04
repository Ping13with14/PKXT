using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 基于Unity原生MatchTarget实现肢体精准目标匹配
/// </summary>
public class ClimbAnimTargetMatch : MonoBehaviour
{
    [Header("环境检测")]
    public PlayerRangeDetector rayCast;
    [Header("翻越动作列表")]
    public List<ClimbAnimSO> climbAnimSOs;
    [Header("攀爬动作列表")]
    public List<ClimbAnimSO> climbUpAnimSOs;


    private Animator _animator;
    // 玩家刚体，负责翻越期间的物理移动
    private Rigidbody _playerRigidbody;
    private ClimbAnimSO _climbAnimSO;
    private int _lastAnimHash;
    private ObstacleHitData _cachedHit;
    public Vector3 _targetPos;
    private bool _hasMatched;

    // 翻越开始时的障碍检测快照：
    // 进入翻越状态瞬间锁定，避免匹配窗口内实时射线失效（角色贴近/越过障碍后前向射线落空）
    // 导致 MatchTarget 不触发、肢体不贴合障碍的"匹配失灵/穿模"问题。
    private ObstacleHitData _climbHitData;
    private bool _hasClimbHitSnapshot;

    private void Awake()
    {
        _animator = GetComponentInParent<Animator>();
        if (_animator == null)
            _animator = GetComponent<Animator>();
        _playerRigidbody = GetComponentInParent<Rigidbody>();
        if (rayCast == null)
            rayCast = GetComponent<PlayerRangeDetector>();
    }

    private void Update()
    {
        if (rayCast == null || _animator == null)
            return;

        _cachedHit = rayCast.ObstacleCheck();
        //CheckCurrentPlayingAnim();
        if (_climbAnimSO != null)
        {
            int currentHash = _animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            if (currentHash != _lastAnimHash)
            {
                _lastAnimHash = currentHash;
                _hasMatched = false;
            }

            // 匹配条件优先使用进入翻越时的检测快照，快照缺失时回退到实时检测
            ObstacleHitData matchHit = _hasClimbHitSnapshot ? _climbHitData : _cachedHit;
            if (!_hasMatched && !_animator.IsInTransition(0)
                && matchHit.forwardHitFound && matchHit.heightHitFound)
            {
                // 只在翻越动画真正开始播放后触发匹配（进度 > 0 且未超过 matchEnd），
                // 避免过渡期/旧动画上用零或旧目标点锁死动画
                float curProgress = _animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                if (curProgress <= 0f || curProgress > _climbAnimSO.matchEnd)
                    return;

                // 将匹配目标点设为障碍物顶部：优先用碰撞体几何推导的顶面前缘点
                // （肢体抓/踩在顶面前缘，更贴合实际障碍，不受射线偏移参数影响），
                // 几何无效时回退高度射线命中点。
                // 注意：不能改用宽度射线命中点（widthHit，前表面向里 1.0m）——
                // 那会把肢体和身体拽到顶面深处（滑动到 1.0m 处）。
                // 匹配期间不再刷新，避免目标点抖动导致模型被拉飞
                _targetPos = matchHit.geometryValid
                    ? matchHit.topFrontEdgePoint
                    : matchHit.heightHit.point;
                // 目标点钳制：高度不低于当前模型位置，防止射线穿透障碍打到地面/后方物体时目标点异常
                _targetPos.y = Mathf.Max(_targetPos.y, transform.position.y + 0.01f);

                // 防御：目标点必须有效（非零且离当前位置合理），否则跳过本帧，等目标点正常后再匹配
                float distToTarget = Vector3.Distance(_targetPos, transform.position);
                if (_targetPos.sqrMagnitude < 0.01f || distToTarget > 5f)
                    return;

                DoTargetMatch();
                _hasMatched = true;
            }
        }
        else
        {
            _lastAnimHash = 0;
            _hasMatched = false;
        }
    }
    //获取检测到的障碍物的高度（使用缓存检测结果）
    public float CheckObstacleHeight()
    {
        return CheckObstacleHeight(_cachedHit);
    }

    //获取检测到的障碍物的高度（使用指定检测结果，保证与调用方同快照）
    public float CheckObstacleHeight(ObstacleHitData hitData)
    {
        if (hitData.forwardHitFound && hitData.heightHitFound)
        {
            return hitData.heightHit.point.y - _animator.transform.position.y;
        }
        return 0f;
    }



    // 由状态进入时传入已匹配的ClimbAnimSO，供后续MatchTarget使用
    public void SetCurrentClimbAnimSO(ClimbAnimSO so)
    {
        // 无快照版本：保持旧行为，实时检测作为匹配依据
        _hasClimbHitSnapshot = false;
        _climbAnimSO = so;
        _hasMatched = false;
    }

    /// <summary>
    /// 进入翻越时传入动作配置与同一次的障碍检测快照，
    /// 匹配目标点与触发条件全部基于该快照，杜绝匹配窗口内实时射线失效。
    /// </summary>
    public void SetCurrentClimbAnimSO(ClimbAnimSO so, ObstacleHitData hitData)
    {
        _climbHitData = hitData;
        _hasClimbHitSnapshot = true;
        _climbAnimSO = so;
        _hasMatched = false;
    }

    public void ClearCurrentClimbAnimSO()
    {
        _climbAnimSO = null;
        _hasMatched = false;
        _lastAnimHash = 0;
        _hasClimbHitSnapshot = false;
    }

    /// <summary>
    /// 枚举转换Unity目标匹配肢体
    /// </summary>
    private AvatarTarget JointToAvatarTarget(BodyJointType joint)
    {
        return joint switch
        {
            BodyJointType.LeftHand => AvatarTarget.LeftHand,
            BodyJointType.RightHand => AvatarTarget.RightHand,
            BodyJointType.LeftFoot => AvatarTarget.LeftFoot,
            BodyJointType.RightFoot => AvatarTarget.RightFoot,
            _ => AvatarTarget.Root
        };
    }

    /// <summary>
    /// 执行官方目标匹配
    /// </summary>
    // 执行目标匹配：启用根运动驱动位移，禁用CC胶囊碰撞体避免拽回模型
    public void DoTargetMatch( )
    {
        _animator.applyRootMotion = true;
        // 关闭刚体碰撞响应，避免 MatchTarget 调整动画时被碰撞体阻挡
        if (_playerRigidbody != null)
            _playerRigidbody.detectCollisions = false;

        AvatarTarget avatarTarget = JointToAvatarTarget(_climbAnimSO.targetJoint);

        _animator.MatchTarget(
            _targetPos,
            _animator.transform.rotation,
            avatarTarget,
            new MatchTargetWeightMask(Vector3.one, 0f),
            _climbAnimSO.matchStart,
            _climbAnimSO.matchEnd
        );
    }
}
