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
        // 处理移动方向
        playerController.MoveDirection();

        #region 水平移动
        if (playerController.inputMoveVec2 != Vector2.zero && playerController.cc.enabled)
        {
            // 水平移动，重力位移由FixedUpdate集中处理
            playerController.cc.Move(playerModel.transform.forward * MoveSpeed * Time.deltaTime);
        }
        #endregion

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

        #region 检测奔跑跳跃
        if (playerController.inputSystem.Player.Jump.triggered)
        {
            playerController.SwitchState(PlayerState.RunningJump);
            return;
        }
        #endregion
    }

    /// <summary>
    /// 是否切换为加速跑；默认不切换
    /// </summary>
    protected virtual bool ShouldSpeedUp() => false;
}
