using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 攀爬动作脚本化
/// </summary>
/// 
[CreateAssetMenu(menuName = "ClimbAnimSO/ClimbAnim")]
public class ClimbAnimSO : ScriptableObject
{
    [Tooltip("动画状态机内动画名称")]
    public string animStateName;

    [Tooltip("MatchTarget匹配开始归一化时间")]
    [Range(0f, 1f)] public float matchStart;
    [Tooltip("MatchTarget匹配结束归一化时间")]
    [Range(0f, 1f)] public float matchEnd;

    [Tooltip("高度差值判定阈值，作为动画切换分支条件")]
    public float minHeight;
    public float maxHeight;

    [Tooltip("指定进行目标匹配的肢体")]
    public BodyJointType targetJoint;
}
