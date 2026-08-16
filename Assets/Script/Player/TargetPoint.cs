using UnityEngine;

/// <summary>
/// 跟随点：保持与玩家水平位置一致，垂直高度偏移在 Awake 时记录。
/// 用于 FreeLook 相机锁定跟随目标。
/// </summary>
public class TargetPoint : MonoBehaviour
{
    //高度
    private float height;

    private void Awake()
    {
        height = transform.position.y;
    }

    private void LateUpdate()
    {
        // 判空保护：PlayerController 可能尚未初始化或已被销毁
        if (PlayerController.INSTANCE == null || PlayerController.INSTANCE.playerModel == null)
            return;

        Vector3 playerPos = PlayerController.INSTANCE.playerModel.transform.position;
        transform.position = new Vector3(playerPos.x, playerPos.y + height, playerPos.z);
    }
}
