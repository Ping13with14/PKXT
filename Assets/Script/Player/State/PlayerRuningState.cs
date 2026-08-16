using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//�������״̬�ű�
public class PlayerRuningState : PlayerStateBase
{
    public override void Enter()
    {
        base.Enter();
        playerController.PlayerAnimation("Running");
    }

    public override void Update()
    {

        base.Update();
        //�����ƶ�����
        playerController.MoveDirection();
        #region 
        if (playerController.inputMoveVec2 != Vector2.zero && playerController.cc.enabled)
        {
            // 水平移动，重力位移由FixedUpdate集中处理
            playerController.cc.Move(playerModel.transform.forward * playerController.RuningSpeed * Time.deltaTime);
        }
        #endregion
        #region 
        if (playerController.inputMoveVec2 == Vector2.zero )
        {
            playerController.SwitchState(PlayerState.Idle);
            return;
        }
        #endregion
        #region 
        if(playerController.AnimationPlayTime > 3 || playerController.inputSystem.Player.SpeedUp.triggered)
        {
            playerController.SwitchState(PlayerState.FastRun);
            return;
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
