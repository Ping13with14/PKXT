using UnityEngine;

/// <summary>
/// 状态基类,所有玩家状态继承此类。Enter时自动获取PlayerController/PlayerModel引用
/// </summary>
public abstract class StateBase
{
    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="owner">宿主</param>
    public abstract void Init(IStateMachineOwner owner);

    /// <summary>
    /// 释放资源
    /// </summary>
    public abstract void UnInit();

    /// <summary>
    /// 进入状态时调用,自动提取PlayerController/PlayerModel供子类使用
    /// </summary>
    public abstract void Enter();


    /// <summary>
    /// 每帧调用,由StateMachine驱动
    /// </summary>
    public abstract void Update();
    public abstract void FixedUpdate();
    public abstract void LateUpdate();


    /// <summary>
    /// 退出状态时调用,自动清理player/model引用
    /// </summary>
    public abstract void Exit();
}
