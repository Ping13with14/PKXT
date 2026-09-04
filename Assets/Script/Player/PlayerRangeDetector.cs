using UnityEngine;

/// <summary>
/// 双层递进射线检测
/// 自身偏移起点向前发射 → 抬高5单位向下获取落地目标点
/// </summary>
public class PlayerRangeDetector : MonoBehaviour
{
   
    public Vector3 forwardRayOffset = new Vector3(0, 0.25f, 0);
    public float forwardRayLength = 0.8f;
    public float heightRayLength = 5;
    public float heightPointTransformOffset = 0.1f;
    public float widthPointTransformOffset = 1f;
    public LayerMask ObstacleLayer;
    [Tooltip("肢体落点沿法线向障碍内侧的偏移量（让手/脚落在顶面内而非边缘外）")]
    public float topEdgeInset = 0.08f;


    public ObstacleHitData ObstacleCheck()
    {   
        var hitData = new ObstacleHitData();

        var forwardOrigin = transform.position + forwardRayOffset;
        hitData.forwardHitFound = Physics.Raycast(forwardOrigin,transform.forward,
            out hitData.forwardHit,forwardRayLength,ObstacleLayer);
        Debug.DrawRay(forwardOrigin, transform.forward * forwardRayLength, (hitData.forwardHitFound) ? Color.red : Color.white);

        // 障碍物实际几何：从命中碰撞体的 bounds 推导顶面高度/尺寸与前缘点，
        // 供高度匹配与肢体落点使用（比高度射线命中点更稳，不受射线偏移参数影响）
        if (hitData.forwardHitFound && hitData.forwardHit.collider != null)
        {
            Bounds bounds = hitData.forwardHit.collider.bounds;
            hitData.obstacleTopY = bounds.max.y;
            hitData.obstacleCenter = bounds.center;
            hitData.obstacleSize = bounds.size;

            Vector3 faceNormal = hitData.forwardHit.normal;
            faceNormal.y = 0f;
            if (faceNormal.sqrMagnitude > 0.0001f)
                faceNormal.Normalize();

            // 顶面前缘点：命中点抬到顶面高度，再沿法线向障碍内侧偏移
            hitData.topFrontEdgePoint = hitData.forwardHit.point;
            hitData.topFrontEdgePoint.y = bounds.max.y;
            hitData.topFrontEdgePoint += faceNormal * topEdgeInset;
            hitData.geometryValid = true;
        }

        if (hitData.forwardHitFound )
        {
            var heightOrigin = hitData.forwardHit.point + Vector3.up * heightRayLength + transform.forward * heightPointTransformOffset;
            hitData.heightHitFound = Physics.Raycast(heightOrigin, Vector3.down,
                out hitData.heightHit, heightRayLength, ObstacleLayer);
            Debug.DrawRay(heightOrigin, Vector3.down * heightRayLength, (hitData.heightHitFound) ? Color.red : Color.white);
        }

        if (hitData.forwardHitFound )
        {
            var widthOrigin = hitData.forwardHit.point + Vector3.up * heightRayLength + transform.forward * widthPointTransformOffset;
            hitData.widthHitFound = Physics.Raycast(widthOrigin, Vector3.down,
                out hitData.widthHit, heightRayLength, ObstacleLayer);
            Debug.DrawRay(widthOrigin,Vector3.down * heightRayLength, (hitData.widthHitFound) ? Color.red : Color.white);
        }

        return hitData;
    }


}
public struct ObstacleHitData
{
    public bool widthHitFound;
    public bool forwardHitFound;
    public bool heightHitFound;
    public RaycastHit forwardHit;
    public RaycastHit heightHit;
    public RaycastHit widthHit;

    // 障碍物实际几何（由 forwardHit.collider.bounds 推导）
    public bool geometryValid;        // 几何信息是否有效
    public float obstacleTopY;        // 顶面世界高度
    public Vector3 obstacleCenter;    // 碰撞体世界中心
    public Vector3 obstacleSize;      // 障碍物世界尺寸
    public Vector3 topFrontEdgePoint; // 顶面前缘点（面向玩家一侧，略向障碍内侧偏移）
}
