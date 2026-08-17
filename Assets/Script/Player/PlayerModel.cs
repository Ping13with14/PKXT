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

    //角色控制器
    public CharacterController characterController;

    //玩家状态
    public PlayerState state;

    //动画根运动每帧位移增量（OnAnimatorMove中捕获，供状态在LateUpdate中使用）
    [HideInInspector] public Vector3 animDeltaPosition;
    //动画根运动每帧旋转增量
    [HideInInspector] public Quaternion animDeltaRotation;

    public void Awake()
    {
        // CC 挂在模型自身（方案B：CC与Animator同物体）
        if (characterController == null)
            characterController = GetComponent<CharacterController>();
        if (_animator == null)
            _animator = GetComponent<Animator>();
        if (_animator == null)
        {
            Debug.LogError("未找到 Animator 组件！模型动画功能将不可用");
            enabled = false;
            return;
        }
        // 默认关闭根运动自动应用；翻越/攀爬时由 DoTargetMatch 临时开启，
        // 位移统一通过 OnAnimatorMove 应用到 CC
        _animator.applyRootMotion = false;
    }

    // 在动画更新后捕获根运动数据，此时deltaPosition/deltaRotation为最新值
    // 执行顺序: Update → 动画系统 → OnAnimatorMove → LateUpdate
    void OnAnimatorMove()
    {
        animDeltaPosition = _animator.deltaPosition;
        animDeltaRotation = _animator.deltaRotation;

        // 方案B：仅当启用根运动时（翻越/攀爬，DoTargetMatch 设置 applyRootMotion=true）
        // 将动画根运动位移应用到 CC，位移与动作同源；其余状态由代码驱动，避免双重位移
        if (_animator.applyRootMotion && characterController != null)
            characterController.Move(animDeltaPosition);
    }

    public bool IsAnimationEnd()
    {
        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        return !_animator.IsInTransition(0) && stateInfo.normalizedTime >= 1f;
    }
}
