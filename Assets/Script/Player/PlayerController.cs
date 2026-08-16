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

    //输入系统
    [HideInInspector] public InputSystem inputSystem;
    //玩家移动输入
    [HideInInspector] public Vector2 inputMoveVec2;
    //移动的三维向量
    [HideInInspector] public Vector3 inputMoveVec3;

    //状态机
    private StateMachine stateMachine;

    //角色控制器
    public CharacterController characterController;

    [Header("翻越位移节奏")]
    [Tooltip("翻越动画进度达到该值后才开始位移(0=全程同步, 0.2=先做前20%动作再移动)")]
    [Range(0f, 0.9f)] public float ClimbMoveStartDelay = 0f;
    [Tooltip("翻越位移速度倍率(1=与动画同步, >1 位移提前完成, <1 位移滞后)")]
    [Range(0.2f, 3f)] public float ClimbMoveSpeedMultiplier = 1f;

    //动画播放时长
    public float AnimationPlayTime = 0;

    //地面检测
    [HideInInspector] public bool isGround;
    //地面层级
    public LayerMask GroundLayer;
    //检测半径
    public float CheckRadius = 0.3f;
    //检测点偏转值
    public Vector3 GroundTestOffset;

    //重力加速度
    public float gravity = -9.8f;
    //垂直方向累计速度
    [HideInInspector] public float verticalVelocity = 0f;
    //重力开关
    public bool hasGravity = true;

    //转向速度
    public float rotationSpeed = 8f;

    protected override void Awake()
    {
        base.Awake();

        stateMachine = new StateMachine(this);
        //实例化输入系统
        inputSystem = new InputSystem();
        mainCamera = Camera.main;
        // 优先从模型取CC引用；Awake时序中父物体先于子物体，可能尚未赋值，需兜底
        characterController = playerModel.characterController;
        if (characterController == null)
            characterController = GetComponent<CharacterController>();
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


    // 地面检测与重力逻辑放在FixedUpdate中，以固定物理步长执行，确保在Update前完成
    public void FixedUpdate()
    {
        if (!hasGravity) return;

        // 重力常量每帧累加，不依赖任何地面检测结果
        verticalVelocity += gravity * Time.fixedDeltaTime;
        // 每帧Y方向应用重力位移，CC碰撞保证模型真实触碰地面
        characterController.Move(Vector3.up * verticalVelocity * Time.fixedDeltaTime);
        // 基于CC真实碰撞结果归零垂直速度，避免球体检测半径导致提前悬空
        if (characterController.isGrounded)
            verticalVelocity = 0f;
        // 球体地面检测仅用于状态切换，不与重力逻辑绑定
        isGround = IsGround();
    }

    public void Update()
    {   //动画播放时长
        AnimationPlayTime += Time.deltaTime;
        MoveInput();
    }

    //输入移动信息
    public void MoveInput()
    {
        //二维向量输入
        inputMoveVec2 = inputSystem.Player.Move.ReadValue<Vector2>().normalized;
        //三维向量转化
        inputMoveVec3 = new Vector3(inputMoveVec2.x, verticalVelocity, inputMoveVec2.y);
    }

    //处理移动方向
    public void MoveDirection()
    {
        #region 处理移动方向
        //获取相机旋转轴Y
        float cameraAxisY = mainCamera.transform.rotation.eulerAngles.y;
        //仅使用水平输入计算朝向，忽略垂直速度分量
        Vector3 horizontalInput = new Vector3(inputMoveVec3.x, 0, inputMoveVec3.z);
        //四元数×向量计算目标方向
        Vector3 targetDic = Quaternion.Euler(0, cameraAxisY, 0) * horizontalInput;
        Quaternion targetQua = Quaternion.LookRotation(targetDic);
        playerModel.transform.rotation = Quaternion.Slerp(playerModel.transform.rotation,
            targetQua, Time.deltaTime * rotationSpeed);
        #endregion
    }

    /// <summary>
    /// 设置模型控制：关闭时保留CC启用，以便cc.Move跟随动画位移
    /// </summary>
    /// <param name="isControl"></param>
    public void SetControl(bool isControl)
    {
        hasGravity = isControl;
        characterController.detectCollisions = isControl;
        // CC始终保持启用，动画位移通过cc.Move驱动，不直接操作transform
    }

    /// <summary>
    /// 地面检测：在脚下位置做球形重叠检测，碰触到地面层即为真
    /// </summary>
    public bool IsGround()
    {
        return Physics.CheckSphere(transform.position + GroundTestOffset, CheckRadius, GroundLayer);
    }

    //检测绘制方法
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position + GroundTestOffset, CheckRadius);
    }

    private void OnEnable()
    {
        //启动输入系统
        inputSystem.Enable();
    }
    private void OnDisable()
    {
        //关闭事件监听
        inputSystem.Disable();
    }
}
