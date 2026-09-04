using UnityEngine;

/// <summary>
/// 模型待机普通待机状态。
/// 作为跳跃/翻越/攀爬等动作结束后的"中立状态"：进入时清零水平速度，
/// 消除动作结束后的残留位移（惯性滑行），再根据输入决定进入奔跑等状态。
/// </summary>
public class PlayerIdleState : PlayerStateBase
{
    public override void Enter()
    {
        base.Enter();
        playerController.PlayAnimation("Idle");
        // 清零水平速度：跳跃/翻越/攀爬结束后先回待机，残留的水平速度在此消除
        playerController.SetHorizontalVelocity(Vector3.zero);
    }

    public override void Update()
    {
        base.Update();
        // 待机期间水平速度保持为零（防御：任何来源的残留位移都被清零）
        playerController.SetHorizontalVelocity(Vector3.zero);

        //播放待机特殊动画
        #region 检测动画播放时长
        if (playerController.AnimationPlayTime > 10)
        {
            playerController.SwitchState(PlayerState.HappyIdle);
            return;
        }

        #endregion

        #region 监听移动（任何方向输入都进入奔跑：输入方向即移动方向）
        if (playerController.inputMoveVec2 != Vector2.zero)
        {
            playerController.SwitchState(PlayerState.Running);
            return;
        }
        #endregion

        #region 检测跳跃/翻越
        if (playerController.inputSystem.Player.Jump.triggered)
        {
            // 障碍高度<=0.5为普通跳跃，否则触发翻越
            if (ClimbAnimTargetMatch == null || ClimbAnimTargetMatch.CheckObstacleHeight() <= 0.5f)
                playerController.SwitchState(PlayerState.RunningJump);
            else
                playerController.SwitchState(PlayerState.ClimbObstacle);
            return;
        }
        #endregion
    }
}
