using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 翻越障碍状态（方案B：根运动驱动）。
/// 动画自带根运动位移，位移与动作同源天然同步。
/// MatchTarget 在 matchStart~matchEnd 窗口把肢体微调到位，
/// PlayerRootMotionDriver 将根运动位移应用到刚体（只位移不旋转）。
/// </summary>
public class PlayerClimbObstacleState : PlayerStateBase
{
    float ObHeight = 0.0f;

    // 高度适配驱动（PlayerRootMotionDriver，挂 Player 根物体），进入时设置、退出时清除
    private PlayerRootMotionDriver _rootMotionDriver;
    // 脚步 IK（挂 Player 根物体）：动作临近结束时提前渐入，避免结束后脚部没入
    private PlayerFootIK _footIK;
    // 动作进度超过该值后提前启用脚步 IK（给权重渐入留时间）
    private const float FeetIKStartProgress = 0.7f;
    // 攀爬收尾脚部"贴线"数据（前缘向内 climbTopInset 的停靠线；仅攀爬用，翻越为 false）
    private bool _isClimbUp;
    private Vector3 _footSnapOrigin;
    private Vector3 _footSnapNormal;

    public override void Enter()
    {
        base.Enter();

        // 统一使用同一次环境检测结果
        var hitData = ClimbAnimTargetMatch.rayCast.ObstacleCheck();
        ObHeight = ClimbAnimTargetMatch.CheckObstacleHeight(hitData);

        // 锁定翻越朝向：利用前向射线命中的障碍物表面法线，角色朝向 = 法线反方向（面向障碍物表面）。
        // 翻越/攀爬期间方向保持不变（配合 PlayerRootMotionDriver 只应用位移不应用旋转，
        // 否则攀爬剪辑的根运动旋转曲线会让根物体逐帧累积旋转导致疯狂旋转）
        LockFacingToObstacle(hitData);

        // 关闭重力与碰撞检测，位移由根运动驱动
        playerController.SetControl(false);
        // 提前开启根运动：在播放翻越动画前就生效，避免 DoTargetMatch 开启过晚导致首帧位移丢失
        playerModel._animator.applyRootMotion = true;
        // 缓存脚步 IK 引用（与 PlayerController 同物体）
        if (_footIK == null)
            _footIK = playerController.GetComponent<PlayerFootIK>();

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

        // 传入动作配置与本次障碍检测快照：匹配目标点/触发条件全部基于快照，
        // 避免翻越过程中实时射线失效导致肢体匹配不上
        ClimbAnimTargetMatch.SetCurrentClimbAnimSO(matchedSO, hitData);
        // 轨迹/高度适配：攀爬=两段式轨迹（贴墙上升后前移到顶），翻越=Y 曲线（先升后落）
        SetupHeightAdaptation(hitData, matchedSO);
        playerController.PlayAnimation(matchedSO.animStateName, 0.0f);
    }

    /// <summary>
    /// 在动作列表中按障碍高度匹配第一个可用的ClimbAnimSO，未匹配返回null
    /// </summary>
    private ClimbAnimSO FindMatchAnimSO(List<ClimbAnimSO> animSOs)
    {
        foreach (var item in animSOs)
        {
            if (ObHeight >= item.minHeight && ObHeight < item.maxHeight)
                return item;
        }
        return null;
    }

    /// <summary>
    /// 锁定朝向：前向射线命中障碍物表面，表面法线朝外（指向玩家），
    /// 角色朝向 = 法线反方向（面向障碍物表面）。仅取水平分量，模型保持直立。
    /// </summary>
    private void LockFacingToObstacle(ObstacleHitData hitData)
    {
        if (!hitData.forwardHitFound || playerModel == null)
            return;

        Vector3 surfaceNormal = hitData.forwardHit.normal;
        surfaceNormal.y = 0f;
        if (surfaceNormal.sqrMagnitude < 0.0001f)
            return;

        playerModel.transform.rotation = Quaternion.LookRotation(-surfaceNormal.normalized);
    }

    /// <summary>
    /// 高度/轨迹适配：让 PlayerRootMotionDriver 覆盖根运动的位移，使其适配实际障碍几何——
    /// 攀爬（有宽度）用"两段式轨迹"（overridePosition）：先贴墙上升到顶（XZ 锁在障碍前表面，
    /// 身体在墙外不穿模），过顶后再前移到顶面；翻越（无宽度）也用全位置曲线（overridePosition）：
    /// XZ 从起点推进到障碍另一侧落点（保证翻得过去），Y 线性升到"顶+余量"再落回起点。
    /// </summary>
    private void SetupHeightAdaptation(ObstacleHitData hitData, ClimbAnimSO matchedSO)
    {
        _rootMotionDriver = playerController.GetComponent<PlayerRootMotionDriver>();
        if (_rootMotionDriver == null || playerController.playerRigidbody == null)
            return;

        float startY = playerController.playerRigidbody.position.y;
        // 目标顶面高度：优先碰撞体几何，回退高度射线测量值
        float topY = hitData.geometryValid ? hitData.obstacleTopY : startY + ObHeight;
        bool climbUp = hitData.widthHitFound; // 与动作列表选择一致：有宽度=攀爬(上到顶)
        _isClimbUp = climbUp;

        if (climbUp)
        {
            SetupClimbUpTrajectory(hitData, topY);
            return;
        }

        // 翻越：全位置曲线——XZ 从起点线性推进到障碍另一侧落点（保证翻得过去，
        // 不依赖剪辑的 XZ 根运动：Jumping 剪辑偏垂直跳跃、水平位移小，且翻越期间
        // 刚体切 kinematic 后起跑动量被清零）；Y 线性升到"障碍顶+余量"（matchEnd 达峰值）
        // 再线性落回起点高度
        float matchEnd = Mathf.Clamp01(matchedSO.matchEnd);
        float peakY = topY + _rootMotionDriver.vaultClearance;

        if (!hitData.geometryValid)
        {
            // 几何无效：退化为只覆盖 Y（XZ 由根运动驱动）
            _rootMotionDriver.overrideY = true;
            _rootMotionDriver.yTargetCurve = (p) =>
            {
                p = Mathf.Clamp01(p);
                if (matchEnd <= 0f)
                    return Mathf.Lerp(startY, peakY, p);
                if (p <= matchEnd)
                    return Mathf.Lerp(startY, peakY, p / matchEnd);
                return Mathf.Lerp(peakY, startY, (p - matchEnd) / (1f - matchEnd));
            };
            return;
        }

        // 障碍前表面法线（水平，朝外指向玩家）与沿法线的障碍深度
        Vector3 faceNormal = hitData.forwardHit.normal;
        faceNormal.y = 0f;
        if (faceNormal.sqrMagnitude < 0.0001f)
            faceNormal = -playerModel.transform.forward;
        faceNormal.Normalize();
        float depth = Mathf.Abs(Vector3.Dot(hitData.obstacleSize, faceNormal));
        // 落点余量：胶囊半径 + 间距，保证落地时在障碍外
        float landMargin = (playerController.playerCollider != null ? playerController.playerCollider.radius : 0.16f) + 0.2f;

        Vector3 startPos = playerController.playerRigidbody.position;
        // 目标点：障碍另一侧（沿 -法线 穿过障碍 + 落点余量），高度回到起点（平地落地）
        Vector3 targetPos = hitData.forwardHit.point - faceNormal * (depth + landMargin);
        targetPos.y = startY;

        _rootMotionDriver.overridePosition = true;
        _rootMotionDriver.positionCurve = (p) =>
        {
            p = Mathf.Clamp01(p);
            // XZ 线性推进到障碍另一侧
            Vector3 xz = Vector3.Lerp(startPos, targetPos, p);
            // Y 线性升-降（保持原观感）
            float y;
            if (matchEnd <= 0f)
                y = Mathf.Lerp(startY, peakY, p);
            else if (p <= matchEnd)
                y = Mathf.Lerp(startY, peakY, p / matchEnd);
            else
                y = Mathf.Lerp(peakY, startY, (p - matchEnd) / (1f - matchEnd));
            return new Vector3(xz.x, y, xz.z);
        };
    }

    /// <summary>
    /// 攀爬两段式轨迹（防穿模）：阶段1 根位置 XZ 锁在障碍前表面外侧（身体贴墙、面向墙），
    /// Y 从起点升到顶面；阶段2 XZ 前移到顶面刚过前缘处（站上顶贴前缘，不滑到障碍中心），
    /// Y 保持顶面高度。开始一小段从触发点快速贴到墙面，避免瞬间位移。
    /// </summary>
    private void SetupClimbUpTrajectory(ObstacleHitData hitData, float topY)
    {
        var driver = _rootMotionDriver;
        float startY = playerController.playerRigidbody.position.y;
        if (!hitData.geometryValid)
        {
            // 几何无效时退化为纯 Y 上升（保持旧行为）
            driver.overrideY = true;
            driver.yTargetCurve = (p) => Mathf.Lerp(startY, topY + driver.climbStandOffset, Mathf.Clamp01(p));
            return;
        }

        Vector3 startPos = playerController.playerRigidbody.position;

        // 障碍前表面法线（水平，朝外指向玩家）
        Vector3 faceNormal = hitData.forwardHit.normal;
        faceNormal.y = 0f;
        if (faceNormal.sqrMagnitude < 0.0001f)
            faceNormal = -playerModel.transform.forward;
        faceNormal.Normalize();

        // 贴墙站位：前表面点沿法线向外退避 climbStandoff（胶囊在墙外）
        Vector3 wallPos = hitData.forwardHit.point - faceNormal * driver.climbStandoff;
        wallPos.y = startY;
        // 顶面目标：刚跨过前缘即停（前表面向里 climbTopInset，可调，默认 0.2），
        // 站上顶贴前缘，不滑到障碍中心
        float edgeStand = driver.climbTopInset;
        Vector3 topTarget = hitData.forwardHit.point - faceNormal * edgeStand;
        topTarget.y = topY + driver.climbStandOffset;

        // 记录脚部"贴线"数据：收尾时让脚水平贴到这条停靠线（仅对齐纵深、保留左右间距）
        _footSnapOrigin = topTarget;
        _footSnapNormal = faceNormal;

        float overP = Mathf.Clamp01(driver.climbOverProgress);
        const float approachBlend = 0.2f; // 开始阶段从触发点快速贴墙的比例

        driver.overridePosition = true;
        driver.positionCurve = (p) =>
        {
            p = Mathf.Clamp01(p);
            // 阶段1：贴墙上升（前 approachBlend 段从 startPos 快速贴到 wallPos）
            float t1 = p / Mathf.Max(0.0001f, overP);
            Vector3 xz1 = Vector3.Lerp(startPos, wallPos,
                Mathf.Clamp01(t1 / Mathf.Max(0.0001f, approachBlend)));
            if (p <= overP)
                return new Vector3(xz1.x, Mathf.Lerp(startY, topY + driver.climbStandOffset, t1), xz1.z);

            // 阶段2：前移到顶面
            float t2 = (p - overP) / Mathf.Max(0.0001f, 1f - overP);
            Vector3 xz2 = Vector3.Lerp(wallPos, topTarget, t2);
            return new Vector3(xz2.x, topY + driver.climbStandOffset, xz2.z);
        };
    }

    public override void Update()
    {
        base.Update();

        // 位移由根运动 + MatchTarget 驱动，这里只处理状态切换。
        // 动作结束先回待机：清零残留速度（Idle.Enter/Update 会归零），
        // 待机状态再根据输入进入奔跑等状态
        if (playerModel.IsAnimationEnd())
        {
            playerController.SwitchState(PlayerState.Idle);
            return;
        }

        // 动作临近结束时提前渐入脚步 IK：让脚在动作结束前就贴合顶面/地面，
        // 避免结束后（IK 权重从 0 渐入的窗口期）脚部没入场景
        if (_footIK != null
            && playerModel._animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= FeetIKStartProgress)
        {
            _footIK.AllowFeetIK(true);
            // 攀爬收尾：同时把脚水平贴到"前缘向内 climbTopInset"的停靠线（翻越不需要）
            if (_isClimbUp)
                _footIK.SetFootSnapTarget(_footSnapOrigin, _footSnapNormal);
        }
    }

    public override void Exit()
    {
        base.Exit();
        ClimbAnimTargetMatch.ClearCurrentClimbAnimSO();
        // 复位脚步 IK 强制开关与贴线目标，恢复正常状态判断
        if (_footIK != null)
        {
            _footIK.AllowFeetIK(false);
            _footIK.ClearFootSnapTarget();
            _footIK = null;
        }
        _isClimbUp = false;
        // 清除轨迹/高度适配，恢复根运动由动画自身驱动
        if (_rootMotionDriver != null)
        {
            _rootMotionDriver.overrideY = false;
            _rootMotionDriver.yTargetCurve = null;
            _rootMotionDriver.overridePosition = false;
            _rootMotionDriver.positionCurve = null;
            _rootMotionDriver = null;
        }
        playerController.SetControl(true);
        // 显式清零速度：kinematic→dynamic 切换后避免残留位移（刚体恢复碰撞响应前清空）
        if (playerController.playerRigidbody != null)
            playerController.playerRigidbody.velocity = Vector3.zero;
        playerModel._animator.applyRootMotion = false;
    }
}
