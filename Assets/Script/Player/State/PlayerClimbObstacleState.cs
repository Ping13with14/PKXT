using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 翻越障碍状态：关闭重力与碰撞检测，CC通过动画根运动位移跟随模型。
/// 动画接近结束时抬升角色到障碍物高度并略微前推，确保 CC 越过障碍。
/// </summary>
public class PlayerClimbObstacleState : PlayerStateBase
{
    float ObHeight = 0.0f;

    // 动画结束前约 15% 时间点施加一次抬升位移，之后由根运动跑完剩余帧
    bool _hasAppliedLift;

    public override void Enter()
    {
        base.Enter();

        ObHeight = ClimbAnimTargetMatch.CheckObstacleHeight();

        // 关闭重力与碰撞检测，CC保持启用，动画位移由cc.Move驱动
        playerController.SetControl(false);

        _hasAppliedLift = false;

        //厚度区分翻越与攀爬动作
        ClimbAnimSO matchedSO = FindMatchAnimSO(
            ClimbAnimTargetMatch.rayCast.ObstacleCheck().widthHitFound
                ? ClimbAnimTargetMatch.climbUpAnimSOs
                : ClimbAnimTargetMatch.climbAnimSOs);

        // 兜底：高度不在任何SO区间时，避免状态卡死，直接切回待机
        if (matchedSO == null)
        {
            Debug.LogWarning($"障碍高度 {ObHeight:F2} 未匹配任何 ClimbAnimSO，取消翻越");
            playerController.SwitchState(PlayerState.Idle);
            return;
        }

        Debug.Log("播放的动画" + matchedSO.animStateName);
        ClimbAnimTargetMatch.SetCurrentClimbAnimSO(matchedSO);
        playerController.PlayAnimation(matchedSO.animStateName, 0.0f);
    }

    /// <summary>
    /// 在动作列表中按障碍高度匹配第一个可用的ClimbAnimSO，未匹配返回null
    /// </summary>
    private ClimbAnimSO FindMatchAnimSO(List<ClimbAnimSO> animSOs)
    {
        foreach (var item in animSOs)
        {
            if (ObHeight > item.minHeight && ObHeight < item.maxHeight)
                return item;
        }
        return null;
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
                playerController.characterController.Move(lift);
                _hasAppliedLift = true;
            }
        }

        // 动画播放结束，根据输入切换到待机或跑步
        if (playerModel.IsAnimationEnd())
        {
            if (playerController.inputMoveVec2 == Vector2.zero)
                playerController.SwitchState(PlayerState.Idle);
            else
                playerController.SwitchState(PlayerState.Running);
        }
    }

    public override void LateUpdate()
    {
        base.LateUpdate();
        if (playerController.characterController.enabled)
            playerController.characterController.Move(playerModel.animDeltaPosition);
    }

    public override void Exit()
    {
        base.Exit();
        playerController.SetControl(true);
        playerModel._animator.applyRootMotion = false;
        playerController.characterController.enabled = true;
    }
}
