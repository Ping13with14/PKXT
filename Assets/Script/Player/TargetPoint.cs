using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetPoint : MonoBehaviour
{
    //¸ß¶È
    private float height;

    private void Awake()
    {
        height = transform.position.y;
    }
    private void LateUpdate()
    {
        Vector3 playerPos = PlayerController.INSTANCE.playerModel.transform.position;
        transform.position = new Vector3(playerPos.x, playerPos.y + height, playerPos.z);
    }
}
