using UnityEngine;

public class PlayerStateBase : StateBase
{
    
    protected PlayerController playerController;

    protected PlayerModel playerModel;

    protected ClimbAnimTargetMatch ClimbAnimTargetMatch;

    public override void Init(IStateMachineOwner owner)
    {
        playerController = (PlayerController)owner;
        playerModel = playerController.playerModel;
        ClimbAnimTargetMatch = playerController.GetComponentInChildren<ClimbAnimTargetMatch>(true);
    } 

    public override void Enter()
    {
    }

    public override void Update(){ }

    public override void Exit() {

    }

    public override void LateUpdate() { }

    public override void UnInit() { }

    public override void FixedUpdate() { }
}
