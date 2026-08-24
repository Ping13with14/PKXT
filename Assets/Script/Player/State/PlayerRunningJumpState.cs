using UnityEngine;

/// <summary>
/// 跑步跳跃状态：跳跃动画为原地动画无根运动位移，位移由参数驱动，重力累计控制下落
/// </summary>
public class PlayerRunningJumpState : PlayerStateBase
{
    [Header("跳跃参数（动画为原地动画时由参数驱动位移）")]
    public float jumpForce = 3f;      // 起跳初速度
    public float jumpDis = 5f;        // 水平位移速度，与跑步速度一致保证跳跃不减速

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
    }

    public override void Update()
    {
        base.Update();
        // 动画结束且落地后才根据输入切换状态，避免半空中跳转
        if (playerModel.IsAnimationEnd() && playerController.isGround)
        {
            if (playerController.inputMoveVec2 != Vector2.zero)
            {
                playerController.SwitchState(PlayerState.Running);
                return;
            }
            else
            {
                playerController.SwitchState(PlayerState.Idle);
                return;
            }
        }
    }

    public override void LateUpdate()
    {
        base.LateUpdate();

        // 跳跃期间保持向前速度，垂直方向交给刚体重力处理
        playerController.SetHorizontalVelocity(
            playerModel.transform.forward * jumpDis);
    }
}
