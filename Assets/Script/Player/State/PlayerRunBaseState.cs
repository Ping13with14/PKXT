using UnityEngine;

/// <summary>
/// 跑动类状态基类：慢跑/快跑共用逻辑。
/// 子类只需提供动画名、移动速度与是否加速的条件。
/// </summary>
public abstract class PlayerRunBaseState : PlayerStateBase
{
    /// <summary>动画状态名</summary>
    protected abstract string AnimName { get; }
    /// <summary>水平移动速度</summary>
    protected abstract float MoveSpeed { get; }
    /// <summary>动画过渡时长</summary>
    protected virtual float TransitionDuration => 0.25f;

    public override void Enter()
    {
        base.Enter();
        playerController.PlayAnimation(AnimName, TransitionDuration);
    }

    public override void Update()
    {
        base.Update();
        // 移动：委托给 PlayerController 当前移动策略（相机相对，输入即方向即移动）
        playerController.MoveByInput(MoveSpeed);

        #region 停止输入
        if (playerController.inputMoveVec2 == Vector2.zero)
        {
            playerController.SwitchState(PlayerState.Idle);
            return;
        }
        #endregion

        #region 加速切换
        if (ShouldSpeedUp())
        {
            playerController.SwitchState(PlayerState.FastRun);
            return;
        }
        #endregion

        #region 检测奔跑跳跃/翻越
        if (playerController.inputSystem.Player.Jump.triggered)
        {
            // 与待机状态一致：前方障碍高度 > 0.5 时触发翻越，否则普通跳跃
            if (ClimbAnimTargetMatch == null || ClimbAnimTargetMatch.CheckObstacleHeight() <= 0.5f)
                playerController.SwitchState(PlayerState.RunningJump);
            else
                playerController.SwitchState(PlayerState.ClimbObstacle);
            return;
        }
        #endregion
    }

    /// <summary>
    /// 是否切换为加速跑；默认不切换
    /// </summary>
    protected virtual bool ShouldSpeedUp() => false;
}
