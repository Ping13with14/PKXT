# PKXT — 第三人称角色控制与翻越系统原型

基于 Unity 2022.3.17f1c1 的第三人称角色控制演示项目，核心玩法为：移动/奔跑/跳跃 + 自动检测障碍并播放翻越/攀爬动画（基于 Animator.MatchTarget 肢体目标匹配）。

## 环境要求

- Unity 2022.3.17f1c1（或同大版本）
- 依赖包：Cinemachine、Input System、ProBuilder（见 `Packages/manifest.json`）

## 操作方式

| 输入 | 动作 |
| --- | --- |
| WASD / 方向键 | 移动（慢跑） |
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
│   ├── PlayerController.cs  # 角色控制：状态切换、输入、移动、重力、地面检测
│   ├── PlayerModel.cs       # 模型表现：Animator 封装、根运动增量捕获
│   ├── PlayerRangeDetector.cs # 双层射线障碍检测（前向 + 高度 + 宽度）
│   ├── TargetPoint.cs       # 相机锁定跟随点
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
3. `ClimbAnimTargetMatch` 在动画播放到配置的 `matchStart ~ matchEnd` 时间段调用 `Animator.MatchTarget`，将指定肢体（手/脚/根骨）精确匹配到障碍物顶部位置
4. 翻越期间关闭重力与碰撞检测，位移由动画根运动经 `cc.Move` 驱动

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
- **动画匹配位置不对**：调整对应 ClimbAnimSO 的 `matchStart/matchEnd` 与 `targetJoint`
- **打包报错**：本项目代码不依赖 UnityEditor 命名空间，可直接 Build Player

## 开发约定

- 所有 C# 源码统一 UTF-8（无 BOM）编码
- 状态类继承 `PlayerStateBase`，需要跑动逻辑的继承 `PlayerRunBaseState`
- 单例通过 `SingleMonoBase<T>` 实现，禁止手动重复挂载
