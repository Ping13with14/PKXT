using UnityEngine;

/// <summary>
/// 双层递进射线检测
/// 自身偏移起点向前发射 → 抬高5单位向下获取落地目标点
/// </summary>
public class PlayerRangeDetector : MonoBehaviour
{
   
    [SerializeField] public Vector3 forwardRayOffset = new Vector3(0, 0.25f, 0);
    public float forwardRayLength = 0.8f;
    public float heightRayLength = 5;
    public float heightPointTransformOffset = 0.1f;
    public float widthPointTransformOffset = 1f;
    public LayerMask ObstacleLayer;


    public ObstacleHitData ObstacleCheck()
    {   
        var hitData = new ObstacleHitData();

        var forwardOrigin = transform.position + forwardRayOffset;
        hitData.forwardHitFound = Physics.Raycast(forwardOrigin,transform.forward,
            out hitData.forwardHit,forwardRayLength,ObstacleLayer);
        Debug.DrawRay(forwardOrigin, transform.forward * forwardRayLength, (hitData.forwardHitFound) ? Color.red : Color.white);

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
}
