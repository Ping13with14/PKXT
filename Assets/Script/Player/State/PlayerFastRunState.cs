using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// 模型加速跑状态
public class PlayerFastRunState : PlayerStateBase
{
    public override void Enter()
    {
        base.Enter();
        playerController.PlayerAnimation("Fast Run",0.8f);
    }

    public override void Update()
    {
        base.Update();

        // 处理移动方向
        playerController.MoveDirection();

        #region 
        if (playerController.inputMoveVec2 != Vector2.zero && playerController.cc.enabled)
        {
            // 水平移动，重力位移由FixedUpdate集中处理
            playerController.cc.Move(playerModel.transform.forward * playerController.FastRunSpeed * Time.deltaTime);
        }
        #endregion

        #region 
        if (playerController.inputMoveVec2 == Vector2.zero)
        {
            playerController.SwitchState(PlayerState.Idle);
        }
        #endregion

        #region 
        if (playerController.inputSystem.Player.Jump.triggered)
        {
            playerController.SwitchState(PlayerState.RuningJump);
            return;
        }
        #endregion

    }
}
