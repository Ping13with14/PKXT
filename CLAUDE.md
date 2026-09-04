# PKXT — 项目记忆（供后续开发会话读取）

Unity 2022.3.17f1c1 第三人称角色控制 + 翻越/攀爬原型。

## 架构总览

- 状态机：`PlayerController`(宿主) → `StateMachine` → `PlayerState`（Idle/Running/FastRun/RunningJump/ClimbObstacle/HappyIdle），Update/FixedUpdate/LateUpdate 由 `MonoManager` 统一驱动
- 物理：Player 根物体挂 Rigidbody + CapsuleCollider + Animator + PlayerController；Model 子物体挂 PlayerModel(网格/骨骼)、PlayerRangeDetector、ClimbAnimTargetMatch、TargetPoint(相机跟随点)
- 翻越：PlayerRangeDetector 双层射线检测 → 按障碍高度/厚度匹配 ClimbAnimSO → Animator.MatchTarget 肢体匹配 → 根运动驱动（翻越期间刚体切 kinematic，位移经 OnAnimatorMove → MovePosition）

## 核心设计决策（勿回退）

1. **移动 = 策略模式（开放-封闭原则，用户明确要求）**：
   - `PlayerController.moveStrategy` 委托 `PlayerMoveStrategy.Move(speed)`；各移动状态只调 `MoveByInput`，不关心具体移动方式
   - 默认 `CameraRelativeMoveStrategy`：输入方向(相机相对) → 模型恒定角速度平滑转向(rotationSpeed) → 沿模型朝向前进（无侧滑/倒退/原地转圈）
   - **新增移动体系 = 新建 `PlayerMoveStrategy` 子类 + 运行时赋值（`moveStrategy = new Xxx(); Init(controller)`），禁止修改现有状态/控制器/已有策略**
2. **相机（历史大坑）**：FreeLook 必须 BindingMode=**SimpleFollowWithWorldUp(5)**。LockToTarget(3) 会让相机绑定角色朝向，形成"角色转→相机转→输入方向变→角色再转"的反馈闭环，表现为"一输入移动视角疯狂转"
3. **相机角度唯一用途**：`CameraRelativeMoveStrategy.FaceInputDirection` 中的标准第三视角映射（W=远离相机），别无他用
4. **物理**：脚本手动重力（`Rigidbody.useGravity=false`）；翻越 SetControl(false) 时 kinematic + detectCollisions=false，恢复时先清速度再还原
5. **地面检测**：检查球位于胶囊体**最低点**（`center - halfHeight`，不是下半球中心），CheckRadius=0.08
6. **障碍检测**：ObstacleLayer 必须含 Layer 6（场景 `m_Bits: 64`），障碍物 Cube 在 Layer 6
7. **根运动**：Animator 在 Player 根上，applyRootMotion 默认 false，翻越时临时开启。**根运动由 `PlayerRootMotionDriver`（Player 根、与 Animator 同物体）显式驱动**：仅 kinematic 时 `MovePosition` 应用位移、**不应用旋转**（攀爬剪辑的根运动旋转曲线会累积成疯狂旋转，骨骼扭转由骨曲线表现）；`animator.deltaPosition` 是"按 Animator 自身朝向（世界 +Z）"的增量，必须旋转到 Model 朝向再应用（Animator 在从不旋转的根上、朝向在会旋转的 Model 子物体上）。**翻越朝向锁定**：`PlayerClimbObstacleState.LockFacingToObstacle` 在进入翻越时把 Model 朝向锁定为"前向射线命中表面法线的反方向"（面向障碍物），翻越期间方向不变。PlayerModel 上的 OnAnimatorMove 不会被 Unity 回调（不同物体），已移除
8. **脚步 IK**：`PlayerFootIK` 必须挂在 Player 根物体（与 Animator 同物体，OnAnimatorIK 只在该物体上回调，不能挂 Model 子物体）；检测层级未指定时自动跟随 `PlayerController.GroundLayer`；跳跃/翻越（非地面、kinematic）自动退出。**抗"吸地"**：maxLiftCorrection 只修正离地很近的脚、heightSpeed 高度平滑过渡、weightSpeed 权重渐入渐出
9. **跳跃方向**：`PlayerRunningJumpState` 在 Enter 时快照起跳方向（有输入=相机相对输入方向，无输入=模型朝向），空中固定该方向不随转向漂移
10. **翻越匹配快照**：`ClimbAnimTargetMatch.SetCurrentClimbAnimSO(so, hitData)` 在进入翻越状态瞬间锁定障碍检测快照，MatchTarget 的目标点与触发条件全部基于快照，不依赖匹配窗口内的实时射线（角色贴近障碍后实时射线会落空导致肢体匹配失灵/穿模）
11. **攀爬剪辑导入设置（勿回退，历史大坑）**：4 个攀爬剪辑（Jumping/StepUp/JumpUp/Sprint To Wall Climb）的 .meta 必须保持 `keepOriginalPositionXZ: 1`、`keepOriginalPositionY: 1`（**解除烘入**，让髋部位移成为位置根运动）且 `keepOriginalOrientation: 0`（**烘入旋转**）。若位置被烘入（=0），剪辑变成"原地动画"，根物体无任何位置根运动 → 根物体/Collider 不随动画移动（拖地、Collider 不动），翻越位移只能靠 MatchTarget 硬拽（抽搐/方向错乱）；若旋转保留（=1），根旋转曲线逐帧累积导致疯狂旋转
12. **高度适配 + 防穿模（借鉴参考项目 Dynamic Parkour System）**：固定剪辑轨迹不随实际障碍高度变化，导致动作与高度不匹配/肢体落点偏高偏低/上升幅度不对/穿模。解法：① `PlayerRangeDetector` 从 `forwardHit.collider.bounds` 推导 `obstacleTopY`/`obstacleCenter`/`topFrontEdgePoint`（几何顶面前缘点），肢体 MatchTarget 目标点优先用它；② `PlayerRootMotionDriver` 支持两种覆盖：`overrideY`（只覆盖 Y，XZ 保留根运动，几何无效时的兜底）；`overridePosition`（全位置曲线，翻越/攀爬都用）：**翻越** = `PlayerClimbObstacleState.SetupHeightAdaptation` 建立曲线——XZ 从起点线性推进到障碍另一侧落点（保证翻得过去，不依赖剪辑 XZ 根运动；Jumping 剪辑偏垂直跳跃水平位移小，且 kinematic 会清零起跑动量），Y 线性升到"顶+余量"（matchEnd 达峰值）再落回；**攀爬** = `SetupClimbUpTrajectory` 两段式——阶段1 XZ 锁在障碍前表面外侧贴墙上升，阶段2 前移到顶面**刚过前缘处**（`climbTopInset`=0.2，站上顶贴前缘，不滑到障碍中心）。肢体匹配目标点统一用顶面前缘点（`topFrontEdgePoint`，几何无效回退 `heightHit.point`）；**勿用宽度射线命中点（widthHit，前表面向里 1.0m）做匹配目标**，会把肢体和身体拽到顶面深处。可调参数：`vaultClearance`/`climbStandOffset`/`climbStandoff`/`climbOverProgress`/`climbTopInset`（PlayerRootMotionDriver）、`topEdgeInset`（PlayerRangeDetector）。注意：翻越曾试过"早升-保持-缓降"曲线（防穿模），观感怪异已回退为线性，个别穿模接受或用 `vaultClearance` 微调
13. **动作结束归零（防残留位移）**：跳跃/翻越/攀爬等动作结束后一律先转 Idle（`PlayerIdleState.Enter/Update` 清零水平速度），Idle 再根据输入进入奔跑等状态；`PlayerClimbObstacleState.Exit` 在 SetControl(true) 后显式清零刚体速度，消除 kinematic→dynamic 切换与落地后的惯性滑行/残留位移
14. **攀爬结束防脚没入（A+B 组合）**：A）`PlayerClimbObstacleState.Update` 在动画进度 ≥ `FeetIKStartProgress`(0.7) 时调 `PlayerFootIK.AllowFeetIK(true)` 提前渐入脚步 IK（动作结束前脚已贴合顶面，避免 IK 权重从 0 渐入的空窗期脚没入），Exit 复位；攀爬（widthHitFound）同时调 `PlayerFootIK.SetFootSnapTarget` 把脚**水平贴到"前缘向内 climbTopInset"的停靠线**（仅对齐纵深、保留左右间距，脚不挂在边缘外）；B）`climbStandOffset`（默认 0.04，下落感明显可再减）让根停在顶面上方略高，配合重力落定，双保险

## 扩展约定（开放-封闭）

- 新功能 = 新类/新资产，不修改既有方法与功能
- 新移动体系 → `PlayerMoveStrategy` 子类
- 新玩家状态 → 继承 `PlayerStateBase`（跑动类继承 `PlayerRunBaseState`）
- 新翻越动作 → `ClimbAnimSO` 资产 + Animator 状态

## 编码约定

- 所有 C# 源码 UTF-8（无 BOM）
- 单例走 `SingleMonoBase<T>`；状态生命周期由状态机管理
- 中文注释
