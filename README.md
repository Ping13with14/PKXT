# PKXT — 第三人称角色控制与翻越系统原型

基于 Unity 2022.3.17f1c1 的第三人称角色控制演示项目，核心玩法为：移动/奔跑/跳跃 + 自动检测障碍并播放翻越/攀爬动画（基于 Animator.MatchTarget 肢体目标匹配）。

## 环境要求

- Unity 2022.3.17f1c1（或同大版本）
- 依赖包：Cinemachine、Input System、ProBuilder（见 `Packages/manifest.json`）

## 操作方式

| 输入 | 动作 |
| --- | --- |
| WASD / 方向键 | 移动（慢跑，相机相对：W=远离相机，S=转身朝相机） |
| Shift | 加速跑（快跑） |
| 空格 | 跳跃；面对障碍物时自动翻越/攀爬 |
| 鼠标 | 相机视角（Cinemachine FreeLook） |

## 代码结构

```
Assets/Script/
├── Base/                    # 基础类
│   ├── StateBase.cs         # 状态抽象基类（Init/Enter/Update/Exit）
│   ├── PlayerStateBase.cs   # 玩家状态基类，自动获取 Controller/Model 引用
│   └── SingleMonoBase.cs    # 单例 MonoBehaviour 基类（重复实例自动销毁）
├── Manager/
│   └── MonoManager.cs       # Update/FixedUpdate/LateUpdate 任务注册中心（自动创建）
├── StateMachine/
│   └── StateMachine.cs      # 有限状态机：状态字典缓存、生命周期驱动
├── Player/
│   ├── PlayerController.cs  # 角色控制：状态切换、输入、移动（策略委托）、重力、地面检测（Rigidbody + CapsuleCollider 驱动）
│   ├── PlayerModel.cs       # 模型表现：Animator 封装
│   ├── PlayerFootIK.cs      # 脚步 IK（OnAnimatorIK）：双脚贴合地面/斜坡，跳跃/翻越自动退出（挂在 Player 根物体）
│   ├── PlayerRootMotionDriver.cs # 根运动显式驱动：kinematic 时按模型朝向把根运动位移应用到刚体（只位移不旋转，挂在 Player 根物体）
│   ├── PlayerRangeDetector.cs # 双层射线障碍检测（前向 + 高度 + 宽度）
│   ├── TargetPoint.cs       # 相机锁定跟随点
│   ├── Move/                # 移动策略（开放-封闭原则：新增移动体系=新增子类，不改现有代码）
│   │   ├── PlayerMoveStrategy.cs         # 移动策略抽象基类
│   │   └── CameraRelativeMoveStrategy.cs # 相机相对移动（默认：输入即方向，平滑转向）
│   └── State/               # 各玩家状态
│       ├── PlayerRunBaseState.cs      # 慢跑/快跑共用基类
│       ├── PlayerIdleState.cs         # 待机
│       ├── PlayerHappyIdleState.cs    # 特殊待机（随机 Happy/Sad Idle）
│       ├── PlayerRunningState.cs      # 慢跑
│       ├── PlayerFastRunState.cs      # 快跑
│       ├── PlayerRunningJumpState.cs  # 跳跃
│       └── PlayerClimbObstacleState.cs # 翻越/攀爬
├── ClimbAnimTargetMatch.cs  # MatchTarget 肢体目标匹配逻辑
└── ClimbAnimationSO/
    ├── ClimbAnimSO.cs       # 翻越动作配置（ScriptableObject）
    ├── ClimbSo/             # 翻越动作配置资产
    └── ClimbUpSo/           # 攀爬（有宽度障碍）动作配置资产
```

## 翻越系统工作原理

1. `PlayerRangeDetector.ObstacleCheck()` 在角色前方发射射线：
   - 前向射线命中障碍物后，在命中点上方 5 单位向下发射高度射线，获得障碍物顶部落点
   - 同时以更远的偏移再发一条宽度射线，判断障碍是否有宽度（区分"翻越"与"攀爬"）
2. `PlayerClimbObstacleState.Enter()` 按检测到的障碍高度，在 `ClimbAnimSO` 列表中匹配 `minHeight < 高度 < maxHeight` 的动画配置
3. `ClimbAnimTargetMatch` 在动画播放到配置的 `matchStart ~ matchEnd` 时间段调用 `Animator.MatchTarget`，将指定肢体（手/脚/根骨）精确匹配到障碍物顶部（目标点优先用碰撞体几何推导的顶面前缘点，不受射线偏移参数影响）
4. 翻越期间关闭重力与碰撞检测（Rigidbody 切换为 kinematic），水平位移由动画根运动经 `PlayerRootMotionDriver`（Player 根物体，与 Animator 同物体）`MovePosition` 驱动（只应用位移、不应用旋转）；进入翻越时朝向锁定为障碍物表面法线反方向，根运动增量旋转到锁定朝向，任意方向翻越方向一致
5. **高度适配 + 防穿模**：`PlayerRootMotionDriver` 提供两种位移覆盖——**翻越**丢弃根运动 Y（XZ 保留根运动），Y 按动画进度先升到"障碍顶+余量"再落回地面；**攀爬**用两段式轨迹（`overridePosition`）：阶段1 XZ 锁在障碍前表面外侧**贴墙上升**（身体不穿墙），阶段2 前移到障碍中心顶面，使上升幅度/肢体落点/穿模都与实际障碍几何精确匹配（可调：`PlayerRootMotionDriver.vaultClearance`、`climbStandOffset`、`climbStandoff`、`climbOverProgress`，`PlayerRangeDetector.topEdgeInset`）

## 新增翻越/攀爬动作的配置流程

1. 在 `Assets/Arts/模型/` 放入动作 FBX，在 Animator（`Assets/Arts/Model.controller`）中添加对应状态
2. 右键创建 `ClimbAnimSO` 资产（菜单 `ClimbAnimSO/ClimbAnim`），填写：
   - `animStateName`：Animator 中的状态名
   - `matchStart` / `matchEnd`：MatchTarget 匹配起止的归一化时间
   - `minHeight` / `maxHeight`：触发该动画的障碍高度区间
   - `targetJoint`：参与目标匹配的肢体
3. 将资产拖入 `Player` 模型上 `ClimbAnimTargetMatch` 组件的 `climbAnimSOs`（翻越）或 `climbUpAnimSOs`（攀爬）列表

## 常见问题

- **翻越无反应**：检查障碍物 Layer 是否在 `PlayerRangeDetector.ObstacleLayer`（默认 Layer 6），且高度落在某个 ClimbAnimSO 的区间内
- **翻越时根物体/Collider 不动（拖地）**：攀爬 FBX 的 Animation 导入设置里 **Root Transform Position XZ/Y 的 "Bake Into Pose" 必须取消勾选**（meta 中 `keepOriginalPositionXZ/Y: 1`），否则剪辑变成"原地动画"，没有位置根运动驱动根物体；Root Transform Rotation 的 "Bake Into Pose" 应勾选（`keepOriginalOrientation: 0`），避免根旋转曲线造成疯狂旋转
- **动画匹配位置不对**：调整对应 ClimbAnimSO 的 `matchStart/matchEnd` 与 `targetJoint`
- **打包报错**：本项目代码不依赖 UnityEditor 命名空间，可直接 Build Player

## 移动体系与扩展（开放-封闭原则）

移动逻辑基于**策略模式**：`PlayerController.moveStrategy` 持有当前移动策略（默认 `CameraRelativeMoveStrategy`），
各移动状态只调用 `PlayerController.MoveByInput(speed)`，不关心具体移动方式。

- **对修改封闭**：新增移动体系时，不修改任何现有状态类、控制器方法或已有策略
- **对扩展开放**：只需
  1. 新建 `PlayerMoveStrategy` 子类，实现 `Move(float speed)`（读取 `controller.inputMoveVec2`，
     写入 `controller.playerRigidbody` 水平速度，垂直分量保留给重力）
  2. 运行时赋值：`playerController.moveStrategy = new MyStrategy(); moveStrategy.Init(playerController);`
     （或替换 `PlayerController.Awake` 中的默认策略）

示例：坦克式移动、直接位移移动、冲刺等均可作为独立策略接入，互不影响。

## 开发约定

- 所有 C# 源码统一 UTF-8（无 BOM）编码
- 状态类继承 `PlayerStateBase`，需要跑动逻辑的继承 `PlayerRunBaseState`
- 单例通过 `SingleMonoBase<T>` 实现，禁止手动重复挂载
- 跳跃/翻越/攀爬等动作结束后**一律先回 Idle**（清零水平速度），再按输入进入奔跑等状态，避免动作结束后的残留位移/惯性滑行

## 脚步 IK（Foot IK）

- 组件 `PlayerFootIK` 挂在 **Player 根物体**（与 Animator 同物体，`OnAnimatorIK` 只在该物体上回调）
- 待机/慢跑/快跑时双脚从脚踝向下射线检测地面，把脚吸附到地面并贴合斜坡法线（`footGroundOffset` 为脚踝到脚底的高度差，按模型微调）
- 摆动脚保护：脚离地超过 `maxLiftCorrection` 时不吸附；跳跃/翻越（空中、kinematic）权重自动平滑退出
- 检测层级默认跟随 `PlayerController.GroundLayer`（Ground + Obstacle），可单独指定
