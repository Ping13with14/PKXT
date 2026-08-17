using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 翻越障碍状态（方案B：根运动驱动）。
/// 动画自带根运动位移，位移与动作同源天然同步。
/// MatchTarget 在 matchStart~matchEnd 窗口把肢体微调到位，
/// PlayerModel.OnAnimatorMove 将根运动位移应用到 CC。
/// </summary>
public class PlayerClimbObstacleState : PlayerStateBase
{
    float ObHeight = 0.0f;

    public override void Enter()
    {
        base.Enter();

        // 统一使用同一次环境检测结果
        var hitData = ClimbAnimTargetMatch.rayCast.ObstacleCheck();
        ObHeight = ClimbAnimTargetMatch.CheckObstacleHeight(hitData);

        // 关闭重力与碰撞检测，位移由根运动驱动
        playerController.SetControl(false);

        // 厚度区分翻越与攀爬动作
        ClimbAnimSO matchedSO = FindMatchAnimSO(
            hitData.widthHitFound
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

        // 位移由根运动 + MatchTarget 驱动，这里只处理状态切换
        if (playerModel.IsAnimationEnd())
        {
            if (playerController.inputMoveVec2 == Vector2.zero)
                playerController.SwitchState(PlayerState.Idle);
            else
                playerController.SwitchState(PlayerState.Running);
        }
    }

    public override void Exit()
    {
        base.Exit();
        playerController.SetControl(true);
        playerModel._animator.applyRootMotion = false;
        playerController.characterController.enabled = true;
    }
}
