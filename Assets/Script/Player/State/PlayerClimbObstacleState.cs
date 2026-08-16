using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 翻越障碍状态：关闭重力与碰撞检测。
/// 攀爬动画为原地动画（无根运动位移），因此由代码在动画播放期间将 CC 从起点匀速移动到目标点，
/// 与动画进度同步，产生平滑的翻越位移。
/// </summary>
public class PlayerClimbObstacleState : PlayerStateBase
{
    [Header("位移节奏（与动画进度的匹配）")]
    [Tooltip("动画进度达到该值后才开始位移。0=全程同步，0.2=先做前20%动作再移动")]
    [Range(0f, 0.9f)] public float MoveStartDelay = 0f;

    [Tooltip("位移速度倍率。1=与动画同步走满；>1 让位移提前完成；<1 让位移滞后于动画")]
    [Range(0.2f, 3f)] public float MoveSpeedMultiplier = 1f;

    float ObHeight = 0.0f;

    // 位移起点（世界坐标）
    Vector3 _startPos;
    // 位移终点（世界坐标）：障碍物顶部 + 前方偏移
    Vector3 _endPos;

    public override void Enter()
    {
        base.Enter();

        ObHeight = ClimbAnimTargetMatch.CheckObstacleHeight();

        // 关闭重力与碰撞检测，CC保持启用，位移由代码匀速驱动
        playerController.SetControl(false);

        // 记录位移起点与终点
        _startPos = playerController.transform.position;
        _endPos = CalcEndPos();

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
    /// 计算位移终点：障碍物顶部命中点，再沿模型前方推出一段距离，确保 CC 越过障碍边缘。
    /// </summary>
    private Vector3 CalcEndPos()
    {
        var hitData = ClimbAnimTargetMatch.rayCast.ObstacleCheck();
        if (hitData.heightHitFound)
        {
            Vector3 top = hitData.heightHit.point;
            // 顶部点 + 前方偏移（跨过障碍物中心），保留障碍高度
            return new Vector3(
                top.x + playerModel.transform.forward.x * 0.6f,
                top.y,
                top.z + playerModel.transform.forward.z * 0.6f);
        }
        // 未检测到顶部时退化为仅抬升
        return playerController.transform.position + Vector3.up * ObHeight;
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
        Vector3 delta = targetPos - playerController.transform.position;
        if (delta.sqrMagnitude > 0.0001f)
            playerController.characterController.Move(delta);

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
    /// 位移进度：在动画进度基础上应用起步延迟与速度倍率。
    /// 延迟后剩余时间按倍率缩放，保证最终都在动画结束时到达终点。
    /// </summary>
    private float GetMoveProgress()
    {
        float animT = GetAnimProgress();
        if (animT <= MoveStartDelay)
            return 0f;
        // 将 [MoveStartDelay, 1] 区间映射到 [0, 1]，再乘速度倍率并截断
        float remapped = (animT - MoveStartDelay) / Mathf.Max(0.001f, 1f - MoveStartDelay);
        return Mathf.Clamp01(remapped * MoveSpeedMultiplier);
    }

    public override void LateUpdate()
    {
        base.LateUpdate();
    }

    public override void Exit()
    {
        base.Exit();
        playerController.SetControl(true);
        playerModel._animator.applyRootMotion = false;
        playerController.characterController.enabled = true;
    }
}
