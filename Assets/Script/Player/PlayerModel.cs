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
        // 位移由 PlayerRootMotionDriver（Player 根物体上，与 Animator 同物体）显式驱动
        _animator.applyRootMotion = false;
    }

    public bool IsAnimationEnd()
    {
        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        return !_animator.IsInTransition(0) && stateInfo.normalizedTime >= 1f;
    }
}
