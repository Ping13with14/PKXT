using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 宿主标记
/// </summary>
public interface IStateMachineOwner { }
/// <summary>
/// 有限状态机,管理状态切换、回退和生命周期驱动
/// </summary>
public class StateMachine
{
    //状态字典
    private readonly Dictionary<Type, StateBase> stateDic = new Dictionary<Type, StateBase>();
    //当前状态
    public StateBase currentState;
    //宿主
    private IStateMachineOwner owner;
    //是否包含当前状态
    public bool HasState { get =>  currentState != null; }

    public StateMachine(IStateMachineOwner owner)
    {
        Init(owner);
    }

    //初始化
    public void Init(IStateMachineOwner owner)
    {
        this.owner = owner;
    }
    /// <summary>
    /// 进入状态
    /// </summary>
    /// <typeparam name="T">状态类型</typeparam>
    public void EnterState<T>() where T : StateBase, new()
    {
        if (HasState && currentState.GetType() == typeof(T))
            return;

        #region 结束当前状态
        if (HasState)
        {
            ExitCurrentState();
        }
        #endregion


        #region 进入新状态
        currentState = LoadState<T>();
        EnterCurrentState();
        #endregion
    }

    /// <summary>
    /// 加载或返回新状态
    /// </summary>
    /// <typeparam name="T">状态类型</typeparam>
    /// <returns></returns>
    private StateBase LoadState<T>() where T : StateBase, new()
    {
        //获取状态类型
        Type stateType = typeof(T);

        //如果字典不存在该状态
        if(!stateDic.TryGetValue(stateType, out StateBase state))
        {
            //创建一个新状态并保存到字典
            state = new T();
            state.Init(owner);
            stateDic.Add(stateType, state);
        }
        return state;
    }

    private void EnterCurrentState()
    {
        if (currentState == null)
            return;
        if (MonoManager.INSTANCE == null)
            MonoManager.AutoCreate();
        currentState.Enter();
        MonoManager.INSTANCE.AddUpdateAction(currentState.Update);
        MonoManager.INSTANCE.AddFixedUpdateAction(currentState.FixedUpdate);
        MonoManager.INSTANCE.AddLateUpdateAction(currentState.LateUpdate);
    }

    private void ExitCurrentState()
    {
        if (currentState == null)
            return;
        currentState.Exit();
        if (MonoManager.INSTANCE == null)
            return;
        MonoManager.INSTANCE.RemoveUpdateAction(currentState.Update);
        MonoManager.INSTANCE.RemoveFixedUpdateAction(currentState.FixedUpdate);
        MonoManager.INSTANCE.RemoveLateUpdateAction(currentState.LateUpdate);
    }

    /// <summary>
    /// 停止运作，释放资源
    /// </summary>
    public void Clear()
    {
        ExitCurrentState();
        foreach (var item in stateDic.Values)
        {
            item.UnInit();
        }
        stateDic.Clear();
    }
}
