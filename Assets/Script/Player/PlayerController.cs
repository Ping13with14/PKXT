using UnityEngine;

/// <summary>
/// 角色控制:状态机管理、输入检测、动画播放
/// </summary>
public class PlayerController : SingleMonoBase<PlayerController>, IStateMachineOwner
{
    public Camera mainCamera;
    //慢跑速度
    public float RunningSpeed = 5f;
    //快速跑速度
    public float FastRunSpeed = 8f;
    //拿到模型
    public PlayerModel playerModel;

    // 移动策略（开放-封闭：新增移动体系时新建 PlayerMoveStrategy 子类并赋值，不改现有代码）
    public PlayerMoveStrategy moveStrategy;

    //输入系统
    [HideInInspector] public InputSystem inputSystem;
    //玩家移动输入
    [HideInInspector] public Vector2 inputMoveVec2;

    //状态机
    private StateMachine stateMachine;

    // 玩家刚体，负责移动、重力和碰撞响应
    public Rigidbody playerRigidbody;
    // 根物体上的胶囊碰撞体，用于以脚底位置进行地面检测
    public CapsuleCollider playerCollider;

    //动画播放时长
    public float AnimationPlayTime = 0;

    //地面检测
    [HideInInspector] public bool isGround;
    //地面层级
    public LayerMask GroundLayer;
    // 地面检测半径只作为脚底缓冲，不再从根物体中心开始检测
    public float CheckRadius = 0.08f;
    //检测点偏转值
    public Vector3 GroundTestOffset;

    // 重力加速度，由脚本手动施加给刚体
    public float gravity = -9.8f;
    //垂直方向累计速度
    [HideInInspector] public float verticalVelocity = 0f;
    //重力开关
    public bool hasGravity = true;

    // 转向角速度（度/秒）：朝向变化的最大角速度，越大越跟手，越小越柔和
    public float rotationSpeed = 360f;

    protected override void Awake()
    {
        base.Awake();
        if (INSTANCE != this)
            return;

        // 输入系统必须在 Start 和所有状态更新之前创建并启用
        inputSystem = new InputSystem();
        inputSystem.Enable();
        mainCamera = Camera.main;
        stateMachine = new StateMachine(this);

        // Animator 与刚体统一挂在 Player 根物体上，根运动会直接移动物理主体
        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody>();
        if (playerRigidbody == null && playerModel != null)
            playerRigidbody = playerModel.GetComponentInParent<Rigidbody>();
        if (playerCollider == null)
            playerCollider = GetComponent<CapsuleCollider>();

        if (playerRigidbody != null)
        {
            // 使用脚本中的 gravity 数值，关闭 Rigidbody 默认重力避免重复施加
            playerRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            playerRigidbody.useGravity = false;
            playerRigidbody.isKinematic = false;
        }

        // 初始化移动策略：未指定时使用默认的相机相对移动
        if (moveStrategy == null)
            moveStrategy = new CameraRelativeMoveStrategy();
        moveStrategy.Init(this);
    }

    public void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        //切换到待机状态
        SwitchState(PlayerState.Idle);
    }

    /// <summary>
    /// 切换状态
    /// </summary>
    /// <param name="playerState">状态</param>
    public void SwitchState(PlayerState playerState)
    {
        switch (playerState)
        {
            case PlayerState.Idle:
                stateMachine.EnterState<PlayerIdleState>();
                break;
            case PlayerState.HappyIdle:
                stateMachine.EnterState<PlayerHappyIdleState>();
                break;
            case PlayerState.Running:
                stateMachine.EnterState<PlayerRunningState>();
                break;
            case PlayerState.FastRun:
                stateMachine.EnterState<PlayerFastRunState>();
                break;
            case PlayerState.RunningJump:
                stateMachine.EnterState<PlayerRunningJumpState>();
                break;
            case PlayerState.ClimbObstacle:
                stateMachine.EnterState<PlayerClimbObstacleState>();
                break;
        }
        if (playerModel != null)
            playerModel.state = playerState;
    }

    /// <summary>
    /// 播放动画
    /// </summary>
    /// <param name="animationName">动画名称</param>
    /// <param name="fixedTransitionDuration">过渡时长</param>
    public void PlayAnimation(string animationName, float fixedTransitionDuration = 0.25f)
    {
        playerModel._animator.CrossFadeInFixedTime(animationName, fixedTransitionDuration);
        AnimationPlayTime = 0;
    }

    // 固定物理步长只更新重力和地面状态，水平移动由各状态委托移动策略写入刚体速度
    public void FixedUpdate()
    {
        if (playerRigidbody == null)
            return;

        // 翻越期间刚体是运动学刚体，只能由 MovePosition 驱动，不能写入 velocity
        if (!playerRigidbody.isKinematic)
        {
            if (hasGravity)
            {
                // 刚体不使用 Unity 默认重力时，由脚本手动累计重力
                verticalVelocity += gravity * Time.fixedDeltaTime;
                Vector3 gravityVelocity = playerRigidbody.velocity;
                gravityVelocity.y = verticalVelocity;
                playerRigidbody.velocity = gravityVelocity;
            }
            else
            {
                verticalVelocity = 0f;
                Vector3 velocityWithoutGravity = playerRigidbody.velocity;
                velocityWithoutGravity.y = 0f;
                playerRigidbody.velocity = velocityWithoutGravity;
            }
        }
        else
        {
            verticalVelocity = 0f;
        }

        isGround = IsGround();
        if (!playerRigidbody.isKinematic && isGround && playerRigidbody.velocity.y < 0f)
        {
            Vector3 groundedVelocity = playerRigidbody.velocity;
            groundedVelocity.y = 0f;
            playerRigidbody.velocity = groundedVelocity;
            verticalVelocity = 0f;
        }
        else
        {
            verticalVelocity = playerRigidbody.velocity.y;
        }
    }

    public void Update()
    {
        //动画播放时长
        AnimationPlayTime += Time.deltaTime;
        MoveInput();
    }

    //读取玩家移动输入
    public void MoveInput()
    {
        // 输入系统在 Awake 中初始化；为空说明当前 PlayerController 未完成初始化
        inputMoveVec2 = inputSystem.Player.Move.ReadValue<Vector2>().normalized;
    }

    /// <summary>
    /// 执行移动：委托给当前移动策略（moveStrategy）。
    /// 由各移动状态每帧调用；新增移动体系时无需改动状态与控制器，仅替换策略实例。
    /// </summary>
    public void MoveByInput(float speed)
    {
        if (moveStrategy != null)
            moveStrategy.Move(speed);
    }

    /// <summary>
    /// 设置玩家物理控制状态。
    /// 翻越期间关闭刚体重力和碰撞响应，根运动仍通过刚体移动。
    /// </summary>
    public void SetControl(bool isControl)
    {
        hasGravity = isControl;
        if (playerRigidbody == null)
            return;

        // 使用脚本中的 gravity 数值，关闭 Rigidbody 默认重力避免重复施加
        playerRigidbody.useGravity = false;
        if (!isControl)
        {
            // 切换为运动学刚体前先清理速度，避免恢复动态刚体时继承旧速度
            playerRigidbody.velocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }
        playerRigidbody.isKinematic = !isControl;
        // 翻越期间关闭碰撞响应，结束后恢复碰撞
        playerRigidbody.detectCollisions = isControl;
    }

    /// <summary>
    /// 使用刚体的水平速度驱动玩家移动（跳跃等状态复用）。
    /// </summary>
    public void SetHorizontalVelocity(Vector3 horizontalVelocity)
    {
        if (playerRigidbody == null || playerRigidbody.isKinematic)
            return;

        Vector3 velocity = playerRigidbody.velocity;
        velocity.x = horizontalVelocity.x;
        velocity.z = horizontalVelocity.z;
        playerRigidbody.velocity = velocity;
    }

    // 地面检测：使用胶囊碰撞体底部附近的小球，避免以根物体中心检测造成悬空感
    public bool IsGround()
    {
        if (playerCollider == null)
            return Physics.CheckSphere(transform.position + GroundTestOffset, CheckRadius, GroundLayer);

        Vector3 worldCenter = playerCollider.transform.TransformPoint(playerCollider.center);
        float worldRadius = playerCollider.radius * Mathf.Max(
            playerCollider.transform.lossyScale.x,
            playerCollider.transform.lossyScale.z);
        float worldHalfHeight = Mathf.Max(
            playerCollider.height * 0.5f * playerCollider.transform.lossyScale.y,
            worldRadius);
        // 脚底 = 胶囊体最低点（center - halfHeight）；检查球放在脚底，半径仅作缓冲
        Vector3 feetPosition = worldCenter + Vector3.down * worldHalfHeight;
        return Physics.CheckSphere(feetPosition + GroundTestOffset, CheckRadius, GroundLayer);
    }

    //检测绘制方法
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(GetGroundCheckPosition(), CheckRadius);
    }

    private Vector3 GetGroundCheckPosition()
    {
        if (playerCollider == null)
            return transform.position + GroundTestOffset;

        Vector3 worldCenter = playerCollider.transform.TransformPoint(playerCollider.center);
        float worldRadius = playerCollider.radius * Mathf.Max(
            playerCollider.transform.lossyScale.x,
            playerCollider.transform.lossyScale.z);
        float worldHalfHeight = Mathf.Max(
            playerCollider.height * 0.5f * playerCollider.transform.lossyScale.y,
            worldRadius);
        // 与 IsGround 保持一致：脚底 = 胶囊体最低点
        return worldCenter + Vector3.down * worldHalfHeight + GroundTestOffset;
    }

    private void OnEnable()
    {
        if (inputSystem != null)
            inputSystem.Enable();
    }
    private void OnDisable()
    {
        if (inputSystem != null)
            inputSystem.Disable();
    }

    protected override void OnDestroy()
    {
        if (stateMachine != null)
            stateMachine.Clear();
        if (inputSystem != null)
        {
            inputSystem.Disable();
            inputSystem.Dispose();
            inputSystem = null;
        }
        base.OnDestroy();
    }
}
