using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ����Unityԭ��MatchTargetʵ��֫�徫׼Ŀ��ƥ��
/// </summary>
public class ClimbAnimTargetMatch : MonoBehaviour
{
    [Header("环境检测")]
    public PlayerRangeDetector rayCast;
    [Header("翻越动作列表")]
    public List<ClimbAnimSO> climbAnimSOs;
    [Header("攀爬动作列表")]
    public List<ClimbAnimSO> climbUpAnimSOs;


    private Animator _animator;
    private CharacterController _cc;
    private ClimbAnimSO _climbAnimSO;
    private int _lastAnimHash;
    private ObstacleHitDate _cachedHit;
    public Vector3 _targetPos;
    private bool _hasMatched;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _cc = GetComponentInParent<CharacterController>();
    }

    private void Update()
    {
        _cachedHit = rayCast.ObscatleCheck();
        //Debug.Log(rayCast.ObscatleCheck().heightHit.point);
        //CheckCurrentPlayingAnim();
        if (_climbAnimSO != null)
        {
            int currentHash = _animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            if (currentHash != _lastAnimHash)
            {
                _lastAnimHash = currentHash;
                _hasMatched = false;
            }
            if (!_hasMatched && !_animator.IsInTransition(0) && _cachedHit.forwardHitFound && _cachedHit.heightHitFound)
            {
                // 将匹配目标点设为障碍物顶部碰撞位置，避免_targetPos默认值Vector3.zero导致瞬移
                _targetPos = _cachedHit.heightHit.point;
                DoTargetMatch();
                _hasMatched = true;
            }
        }
        else
        {
            _lastAnimHash = 0;
            _hasMatched = false;
        }
    }
    //��ȡ��⵽���ϰ���ĸ߶�
    public float CheckObscatleHeight()
    {
        if (_cachedHit.forwardHitFound && _cachedHit.heightHitFound)
        {
            return _cachedHit.heightHit.point.y - transform.position.y;
        }
        return 0f;
    }


    
    // 由状态进入时传入已匹配的ClimbAnimSO，供后续MatchTarget使用
    public void SetCurrentClimbAnimSO(ClimbAnimSO so)
    {
        _climbAnimSO = so;
        _hasMatched = false;
    }

    /// <summary>
    /// ö��תUnity����ƥ��֫��
    /// </summary>
    private AvatarTarget JointToAvatarTarget(BodyJointType joint)
    {
        return joint switch
        {
            BodyJointType.LeftHand => AvatarTarget.LeftHand,
            BodyJointType.RightHand => AvatarTarget.RightHand,
            BodyJointType.LeftFoot => AvatarTarget.LeftFoot,
            BodyJointType.RightFoot => AvatarTarget.RightFoot,
            _ => AvatarTarget.Root
        };
    }

    /// <summary>
    /// ִ�йٷ�Ŀ��ƥ��
    /// </summary>
    // 执行目标匹配：启用根运动驱动位移，禁用CC胶囊碰撞体避免拽回模型
    public void DoTargetMatch( )
    {
        _animator.applyRootMotion = true;
        // 仅关闭碰撞检测，保持 CC 启用但避免胶囊体拽回模型；LateUpdate 检测 applyRootMotion 跳过 cc.Move 防止叠加
        if (_cc != null) _cc.detectCollisions = false;

        AvatarTarget avatarTarget = JointToAvatarTarget(_climbAnimSO.targetJoint);

        _animator.MatchTarget(
            _targetPos,
            transform.rotation,
            avatarTarget,
            new MatchTargetWeightMask(Vector3.one, 0f),
            _climbAnimSO.matchStart,
            _climbAnimSO.matchEnd
        );
    }
}
