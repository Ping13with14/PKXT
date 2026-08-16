using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 模型特殊待机状态
/// </summary>
public class PlayerHappyIdleState : PlayerStateBase
{

    public override void Enter()
    {
        base.Enter();
        int randomNum = Random.Range(0, 2);
        if (randomNum == 0)
        {
            playerController.PlayerAnimation("Happy Idle", 1f);
        }
        else
        {
            playerController.PlayerAnimation("Sad Idle");
        }

    }

    public override void Update()
    {
        base.Update();
        #region 检测动画播放结束
        if(playerModel.IsAnimationEnd())
        {
            playerController.SwitchState(PlayerState.Idle);
            return;
        }
        #endregion

        #region 监听奔跑
        if (playerController.inputMoveVec2 != Vector2.zero)
        {
            playerController.SwitchState(PlayerState.Runing);
            return;
        }
        #endregion

        #region 检测奔跑跳

        #endregion
    }
}
