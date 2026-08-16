using UnityEngine;

/// <summary>
/// 模型慢跑状态
/// </summary>
public class PlayerRunningState : PlayerRunBaseState
{
    protected override string AnimName => "Running";
    protected override float MoveSpeed => playerController.RunningSpeed;

    /// <summary>
    /// 跑动超过3秒或按下加速键时切换为加速跑
    /// </summary>
    protected override bool ShouldSpeedUp() =>
        playerController.AnimationPlayTime > 3 || playerController.inputSystem.Player.SpeedUp.triggered;
}
