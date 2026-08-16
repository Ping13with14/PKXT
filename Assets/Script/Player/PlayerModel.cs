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
        // CC 挂在父物体 Player 上，模型为子物体，需向上查找
        if (characterController == null)
            characterController = GetComponentInParent<CharacterController>();
        if (_animator == null)
            _animator = GetComponent<Animator>();
        if (_animator == null)
        {
            Debug.LogError("未找到 Animator 组件！模型动画功能将不可用");
            enabled = false;
            return;
        }
        // 关闭根运动自动应用，所有位移统一通过cc.Move驱动，避免动画直接改Transform导致与CC错位
        _animator.applyRootMotion = false;
    }

    // 在动画更新后捕获根运动数据，此时deltaPosition/deltaRotation为最新值
    // 执行顺序: Update → 动画系统 → OnAnimatorMove → LateUpdate
    void OnAnimatorMove()
    {
        animDeltaPosition = _animator.deltaPosition;
        animDeltaRotation = _animator.deltaRotation;

        // 【临时诊断】只在翻越动画播放中输出，避免结束后的刷屏。诊断完成后删除。
        if (state == PlayerState.ClimbObstacle)
        {
            float norm = _animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            if (norm < 0.98f)
                Debug.Log($"[ClimbDiag] 帧:{Time.frameCount} 动画进度:{norm:F2} applyRootMotion:{_animator.applyRootMotion} " +
                          $"delta:{animDeltaPosition} 模型位置:{transform.position}");
        }
    }

    public bool IsAnimationEnd()
    {
        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        return !_animator.IsInTransition(0) && stateInfo.normalizedTime >= 1f;
    }
}
