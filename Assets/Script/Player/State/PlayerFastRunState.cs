using UnityEngine;

/// <summary>
/// 模型加速跑状态
/// </summary>
public class PlayerFastRunState : PlayerRunBaseState
{
    protected override string AnimName => "Fast Run";
    protected override float TransitionDuration => 0.8f;
    protected override float MoveSpeed => playerController.FastRunSpeed;
}
