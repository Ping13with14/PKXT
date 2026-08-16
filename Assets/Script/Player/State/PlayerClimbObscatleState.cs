using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 翻越障碍状态：关闭重力与碰撞检测，CC通过动画根运动位移跟随模型。
/// 动画接近结束时抬升角色到障碍物高度并略微前推，确保 CC 越过障碍。
/// </summary>
public class PlayerClimbObscatleState : PlayerStateBase
{
    float ObHeight = 0.0f;

    // 动画结束前约 15% 时间点施加一次抬升位移，之后由根运动跑完剩余帧
    bool _hasAppliedLift;

    public override void Enter()
    {
        base.Enter();

        ObHeight = ClimbAnimTargetMatch.CheckObscatleHeight();

        // 关闭重力与碰撞检测，CC保持启用，动画位移由cc.Move驱动
        playerController.SetControl(false);

        _hasAppliedLift = false;

        //厚度区分翻羽与攀爬动作
        if (ClimbAnimTargetMatch.rayCast.ObscatleCheck().widthHitFound)
        {
            //攀爬逻辑
            foreach (var item in ClimbAnimTargetMatch.climbUpAnimSOs)
            {
                if (ObHeight > item.minHeight && ObHeight < item.maxHeight)
                {
                    Debug.Log("播放的动画" + item.animStateName);
                    ClimbAnimTargetMatch.SetCurrentClimbAnimSO(item);
                    playerController.PlayerAnimation(item.animStateName, 0.0f);
                    break;
                }
            }
        }
        else
        {
            //翻越逻辑
            foreach (var item in ClimbAnimTargetMatch.climbAnimSOs)
            {
                if (ObHeight > item.minHeight && ObHeight < item.maxHeight)
                {
                    Debug.Log("播放的动画" + item.animStateName);
                    ClimbAnimTargetMatch.SetCurrentClimbAnimSO(item);
                    playerController.PlayerAnimation(item.animStateName, 0.0f);
                    break;
                }
            }
        }
    }

    public override void Update()
    {
        base.Update();

        // 动画接近尾声时，将角色整体抬升到障碍高度并略向前推，帮助 CC 越过障碍边缘。
        // 此时碰撞已关闭，cc.Move 直接生效；抬升在动画结束前完成，剩余帧由根运动微调。
        if (!_hasAppliedLift)
        {
            var stateInfo = playerModel._animator.GetCurrentAnimatorStateInfo(0);
            if (!playerModel._animator.IsInTransition(0) && stateInfo.normalizedTime >= 0.85f)
            {
                Vector3 lift = Vector3.up * ObHeight + playerModel.transform.forward * 0.3f;
                playerController.cc.Move(lift);
                _hasAppliedLift = true;
            }
        }

        // 动画播放结束，根据输入切换到待机或跑步
        if (playerModel.IsAnimationEnd())
        {
            if (playerController.inputMoveVec2 == Vector2.zero)
                playerController.SwitchState(PlayerState.Idle);
            else
                playerController.SwitchState(PlayerState.Runing);
        }
    }

    public override void LateUpdate()
    {
        base.LateUpdate();
        if (playerController.cc.enabled)
            playerController.cc.Move(playerModel.animDeltaPosition);
    }

    public override void Exit()
    {
        base.Exit();
        playerController.SetControl(true);
        playerModel._animator.applyRootMotion = false;
        playerController.cc.enabled = true;
    }
}
