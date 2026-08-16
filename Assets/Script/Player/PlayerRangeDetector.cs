using UnityEngine;

/// <summary>
/// 双层递进射线检测
/// 自身偏移起点向前发射 → 抬高5单位向下获取落地目标点
/// </summary>
public class PlayerRangeDetector : MonoBehaviour
{
   
    [SerializeField] public Vector3 forwardRayOffest = new Vector3(0, 0.25f, 0);
    public float forwardRayLength = 0.8f;
    public float heightRayLength = 5;
    public float heightPointTransformOffest = 0.1f;
    public float widthPointTransformOffest = 1f;
    public LayerMask ObstacleLayer;


    public ObstacleHitDate ObscatleCheck()
    {   
        var hitDate = new ObstacleHitDate();

        var forwardOrigin = transform.position + forwardRayOffest;
        hitDate.forwardHitFound = Physics.Raycast(forwardOrigin,transform.forward,
            out hitDate.forwardHit,forwardRayLength,ObstacleLayer);
        Debug.DrawRay(forwardOrigin, transform.forward * forwardRayLength, (hitDate.forwardHitFound) ? Color.red : Color.white);

        if (hitDate.forwardHitFound )
        {
            var heightOrigin = hitDate.forwardHit.point + Vector3.up * heightRayLength + transform.forward * heightPointTransformOffest;
            hitDate.heightHitFound = Physics.Raycast(heightOrigin, Vector3.down,
                out hitDate.heightHit, heightRayLength, ObstacleLayer);
            Debug.DrawRay(heightOrigin, Vector3.down * heightRayLength, (hitDate.heightHitFound) ? Color.red : Color.white);
        }

        if (hitDate.forwardHitFound )
        {
            var widthOrigin = hitDate.forwardHit.point + Vector3.up * heightRayLength + transform.forward * widthPointTransformOffest;
            hitDate.widthHitFound = Physics.Raycast(widthOrigin, Vector3.down,
                out hitDate.widthHit, heightRayLength, ObstacleLayer);
            Debug.DrawRay(widthOrigin,Vector3.down * heightRayLength, (hitDate.widthHitFound) ? Color.red : Color.white);
        }

        return hitDate;
    }


}
public struct ObstacleHitDate
{
    public bool widthHitFound;
    public bool forwardHitFound;
    public bool heightHitFound;
    public RaycastHit forwardHit;
    public RaycastHit heightHit;
    public RaycastHit widthHit;
}

