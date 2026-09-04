using UnityEngine;

/// <summary>
/// 跑步跳跃状态：跳跃动画为原地动画无根运动位移，位移由参数驱动，重力累计控制下落。
/// 起跳方向在 Enter 时快照：有输入时取输入方向（相机相对，与移动策略一致，转向期间跳跃不偏），
/// 无输入时保持模型面朝方向，保证空中飞行方向稳定不漂移。
/// </summary>
public class PlayerRunningJumpState : PlayerStateBase
{
    [Header("跳跃参数（动画为原地动画时由参数驱动位移）")]
    public float jumpForce = 3f;      // 起跳初速度
    public float jumpDis = 5f;        // 水平位移速度，与跑步速度一致保证跳跃不减速

    // 起跳方向快照（Enter 时锁定，空中不随转向/输入变化）
    private Vector3 _jumpDir = Vector3.forward;

    public override void Enter()
    {
        base.Enter();
        // 起跳时赋予初始垂直速度，FixedUpdate 会继续累计脚本重力
        playerController.verticalVelocity = jumpForce;
        if (playerController.playerRigidbody != null)
        {
            Vector3 velocity = playerController.playerRigidbody.velocity;
            velocity.y = jumpForce;
            playerController.playerRigidbody.velocity = velocity;
        }
        playerController.PlayAnimation("Running Jump");

        // 锁定起跳方向：输入非零时按相机相对输入方向（W=远离相机），否则沿用模型面朝方向
        Vector2 input = playerController.inputMoveVec2;
        if (input != Vector2.zero)
        {
            Camera cam = playerController.mainCamera;
            float cameraYaw = cam != null ? cam.transform.rotation.eulerAngles.y : 0f;
            Vector3 inputDir = Quaternion.Euler(0f, cameraYaw, 0f)
                * new Vector3(input.x, 0f, input.y);
            _jumpDir = inputDir.normalized;
        }
        else
        {
            Vector3 forward = playerModel.transform.forward;
            forward.y = 0f;
            _jumpDir = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }
    }

    public override void Update()
    {
        base.Update();
        // 动画结束且落地后才切换状态，避免半空中跳转。
        // 先回待机清零残留水平速度（Idle.Enter/Update 会归零），
        // 待机状态再根据输入进入奔跑等状态
        if (playerModel.IsAnimationEnd() && playerController.isGround)
        {
            playerController.SwitchState(PlayerState.Idle);
            return;
        }
    }

    public override void LateUpdate()
    {
        base.LateUpdate();

        // 跳跃期间保持起跳方向水平速度，垂直方向交给刚体重力处理
        playerController.SetHorizontalVelocity(_jumpDir * jumpDis);
    }
}
