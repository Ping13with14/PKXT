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
7. **根运动**：Animator 在 Player 根上，applyRootMotion 默认 false，翻越时临时开启；OnAnimatorMove 仅在 kinematic 时手动 MovePosition（脚本与 Animator 不同物体时 Unity 不回调，勿依赖）

## 扩展约定（开放-封闭）

- 新功能 = 新类/新资产，不修改既有方法与功能
- 新移动体系 → `PlayerMoveStrategy` 子类
- 新玩家状态 → 继承 `PlayerStateBase`（跑动类继承 `PlayerRunBaseState`）
- 新翻越动作 → `ClimbAnimSO` 资产 + Animator 状态

## 编码约定

- 所有 C# 源码 UTF-8（无 BOM）
- 单例走 `SingleMonoBase<T>`；状态生命周期由状态机管理
- 中文注释
