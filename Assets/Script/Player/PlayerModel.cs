using UnityEngine;


/// <summary>
/// 角色匹配肢体枚举
/// </summary>
public enum BodyJointType
{
    Root,       // 根骨骼
    LeftHand,   // 左手
    RightHand,  // 右手
    LeftFoot,   // 左脚
    RightFoot   // 右脚
}

/// <summary>
/// 玩家动画状态枚举,用于状态切换时判断
/// </summary>
public enum PlayerState
{
    Idle, HappyIdle, Running, FastRun, RunningJump, ClimbObstacle
}

/// <summary>
/// 角色模型表现:挂载Animator,直接播放动画
/// </summary>
public class PlayerModel : MonoBehaviour
{
    //动画控制器
    public Animator _animator;

    // 玩家刚体，挂在 Player 根物体上
    public Rigidbody playerRigidbody;

    //玩家状态
    public PlayerState state;

    // 动画根运动每帧位移增量，由 PlayerController 在 LateUpdate 中驱动根物体
    [HideInInspector] public Vector3 animDeltaPosition;
    // 动画根运动每帧旋转增量
    [HideInInspector] public Quaternion animDeltaRotation;

    public void Awake()
    {
        // Animator 位于 Player 根物体上
        if (_animator == null)
            _animator = GetComponentInParent<Animator>();
        if (_animator == null)
        {
            Debug.LogError("未找到 Animator 组件！模型动画功能将不可用");
            enabled = false;
            return;
        }
        // 默认关闭根运动自动应用；翻越/攀爬时由状态临时开启，
        // 位移统一由 PlayerController 驱动物理根物体
        _animator.applyRootMotion = false;
    }

    void OnAnimatorMove()
    {
        animDeltaPosition = _animator.deltaPosition;
        animDeltaRotation = _animator.deltaRotation;

        // kinematic 时（翻越状态）Unity 不会自动应用根运动，需手动驱动刚体
        if (playerRigidbody != null && playerRigidbody.isKinematic)
            playerRigidbody.MovePosition(playerRigidbody.position + animDeltaPosition);
    }

    public bool IsAnimationEnd()
    {
        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        return !_animator.IsInTransition(0) && stateInfo.normalizedTime >= 1f;
    }
}
