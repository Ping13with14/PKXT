using UnityEngine;

/// <summary>
/// 相机相对移动（标准第三视角，默认策略）：
/// 输入方向（相机相对）→ 模型以恒定角速度平滑转向该方向 → 沿模型朝向前进。
/// W=远离相机前进，S=转身朝相机前进，A/D=左右侧向跑动。
/// 角色永远正面朝前跑，不会侧滑/倒退/原地转圈。相机角度仅用于标准第三视角映射。
/// </summary>
public class CameraRelativeMoveStrategy : PlayerMoveStrategy
{
    public override void Move(float speed)
    {
        if (controller == null || controller.playerRigidbody == null
            || controller.playerRigidbody.isKinematic)
            return;

        Vector3 velocity = controller.playerRigidbody.velocity;
        if (controller.inputMoveVec2 != Vector2.zero)
        {
            // 1) 模型平滑转向输入方向（恒定角速度，帧率无关）
            FaceInputDirection();

            // 2) 沿面朝方向前进（永不后退/侧滑）
            Vector3 forward = model != null ? model.transform.forward : Vector3.forward;
            forward.y = 0f;
            forward.Normalize();
            velocity.x = forward.x * speed;
            velocity.z = forward.z * speed;
        }
        else
        {
            // 无输入时清除水平速度，避免刚体惯性滑行
            velocity.x = 0f;
            velocity.z = 0f;
        }
        controller.playerRigidbody.velocity = velocity;
    }

    /// <summary>
    /// 面向输入方向（相机相对）：以恒定角速度平滑转向，走最短弧线、不超调。
    /// </summary>
    public void FaceInputDirection()
    {
        if (model == null || controller.inputMoveVec2 == Vector2.zero)
            return;

        Camera mainCamera = controller.mainCamera;
        float cameraAxisY = mainCamera != null ? mainCamera.transform.rotation.eulerAngles.y : 0f;
        Vector3 moveDir = Quaternion.Euler(0f, cameraAxisY, 0f)
            * new Vector3(controller.inputMoveVec2.x, 0f, controller.inputMoveVec2.y);
        moveDir.Normalize();
        Quaternion targetQua = Quaternion.LookRotation(moveDir);
        model.transform.rotation = Quaternion.RotateTowards(
            model.transform.rotation, targetQua, controller.rotationSpeed * Time.deltaTime);
    }
}
