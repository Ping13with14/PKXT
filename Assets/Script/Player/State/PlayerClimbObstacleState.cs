using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 翻越障碍状态：关闭重力与碰撞检测。
/// 攀爬动画为原地动画（无根运动位移），因此由代码在动画播放期间将 CC 从起点匀速移动到目标点，
/// 与动画进度同步，产生平滑的翻越位移。
/// </summary>
public class PlayerClimbObstacleState : PlayerStateBase
{
    // 位移节奏参数在 PlayerController 组件上配置（状态类非MonoBehaviour，无法在Inspector显示字段）
    float ObHeight = 0.0f;

    // CC 中心已加高的标记（在动画约0.6进度时减回）
    bool _isCenterLifted;

    // 位移起点（世界坐标）
    Vector3 _startPos;
    // 位移终点（世界坐标）：障碍物顶部 + 前方偏移
    Vector3 _endPos;

    public override void Enter()
    {
        base.Enter();

        // 统一使用同一次环境检测结果，避免 ObHeight 与 _endPos 来自不同快照
        var hitData = ClimbAnimTargetMatch.rayCast.ObstacleCheck();
        ObHeight = ClimbAnimTargetMatch.CheckObstacleHeight(hitData);

        // 翻越期间临时抬高 CC 中心到障碍物高度，动画约0.6进度时减回
        _isCenterLifted = true;
        playerController.characterController.center += Vector3.up * Mathf.Max(ObHeight, 0f);

        // 关闭重力与碰撞检测，CC保持启用，位移由代码匀速驱动
        playerController.SetControl(false);

        // 记录位移起点与终点（CC 挂在模型上，基准用模型位置而非 Player 根）
        _startPos = playerModel.transform.position;
        _endPos = CalcEndPos(hitData);

        //厚度区分翻越与攀爬动作
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
    /// 计算位移终点：障碍物顶部命中点，再沿模型前方推出一段距离。
    /// 加入钳制保护：高度不低于起点（防检测到地面/异常值下坠）、水平距离限制在合理范围（防检测到远处物体导致飞出去）。
    /// </summary>
    private Vector3 CalcEndPos(ObstacleHitData hitData)
    {
        Vector3 start = _startPos;
        Vector3 forward = playerModel.transform.forward;

        if (hitData.heightHitFound)
        {
            Vector3 top = hitData.heightHit.point;
            Vector3 end = top + forward * 0.6f;

            // 钳制1：终点高度不低于起点（射线可能穿透障碍物打到地面/后方物体）
            end.y = Mathf.Max(end.y, start.y + 0.01f);

            // 钳制2：水平位移限制在 [0.3, 1.2] 之间，防止检测到远处物体时瞬移
            Vector3 horizontal = end - start;
            horizontal.y = 0f;
            float dist = horizontal.magnitude;
            if (dist < 0.3f)
                horizontal = horizontal.normalized * 0.3f;
            else if (dist > 1.2f)
                horizontal = horizontal.normalized * 1.2f;

            end = start + horizontal + Vector3.up * (end.y - start.y);
            return end;
        }

        // 检测失败时退化为仅小幅抬升（不移动水平位置），避免目标点异常
        return start + Vector3.up * Mathf.Max(ObHeight, 0.01f);
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

        // 按"位移进度"移动 CC：位移进度由动画进度经延迟/倍率换算而来，
        // 使位移节奏与动画动作匹配（可先做动作后移动，或让位移提前完成）
        float moveT = GetMoveProgress();
        Vector3 targetPos = Vector3.Lerp(_startPos, _endPos, moveT);
        // 基准用模型位置（CC 挂在模型上），避免与 Player 根位置错位导致位移叠加
        Vector3 delta = targetPos - playerModel.transform.position;
        if (delta.sqrMagnitude > 0.0001f)
            playerController.characterController.Move(delta);

        // 动画进度约0.6时把 CC 中心减回原位（简单实现，无需存储原始值）
        if (_isCenterLifted && GetAnimProgress() >= 0.6f)
        {
            playerController.characterController.center -= Vector3.up * Mathf.Max(ObHeight, 0f);
            _isCenterLifted = false;
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

    /// <summary>
    /// 当前动画归一化进度：过渡结束后从 0 递增到 1
    /// </summary>
    private float GetAnimProgress()
    {
        var stateInfo = playerModel._animator.GetCurrentAnimatorStateInfo(0);
        if (playerModel._animator.IsInTransition(0))
            return 0f;
        return Mathf.Clamp01(stateInfo.normalizedTime);
    }

    /// <summary>
    /// 位移进度：在动画进度基础上应用起步延迟与速度倍率（参数来自 PlayerController 组件）。
    /// 延迟后剩余时间按倍率缩放，保证最终都在动画结束时到达终点。
    /// </summary>
    private float GetMoveProgress()
    {
        float animT = GetAnimProgress();
        float delay = playerController.ClimbMoveStartDelay;
        float multiplier = playerController.ClimbMoveSpeedMultiplier;
        if (animT <= delay)
            return 0f;
        // 将 [delay, 1] 区间映射到 [0, 1]，再乘速度倍率并截断
        float remapped = (animT - delay) / Mathf.Max(0.001f, 1f - delay);
        return Mathf.Clamp01(remapped * multiplier);
    }

    public override void LateUpdate()
    {
        base.LateUpdate();
    }

    public override void Exit()
    {
        base.Exit();
        // CC 中心已在动画约0.6进度时恢复，这里只恢复控制
        playerController.SetControl(true);
        playerModel._animator.applyRootMotion = false;
        playerController.characterController.enabled = true;
    }
}
