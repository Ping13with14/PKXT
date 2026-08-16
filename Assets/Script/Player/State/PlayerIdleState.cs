using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 模型待机普通待机状态
/// </summary>
public class PlayerIdleState : PlayerStateBase
{
    public override void Enter()
    {
        base.Enter();
        playerController.PlayAnimation("Idle");
    }

    public override void Update()
    {
        base.Update();
        //播放待机特殊动画
        #region 检测动画播放时长
        if (playerController.AnimationPlayTime > 10)
        {
            playerController.SwitchState(PlayerState.HappyIdle);
            return;
        }

        #endregion

        #region 监听奔跑
        if (playerController.inputMoveVec2 != Vector2.zero)
        {
            playerController.SwitchState(PlayerState.Running);
            return;
        }
        #endregion

        #region 检测跳跃
        if (playerController.inputSystem.Player.Jump.triggered && ClimbAnimTargetMatch.CheckObstacleHeight() <= 0.5)
        {
            playerController.SwitchState(PlayerState.RunningJump);
            return;
        }
        #endregion

        #region 检测奔跑跳
        if (playerController.inputSystem.Player.Jump.triggered && ClimbAnimTargetMatch.CheckObstacleHeight() > 0.5)
        {
            playerController.SwitchState(PlayerState.ClimbObstacle);
            return;
        }
        #endregion
    }
}
