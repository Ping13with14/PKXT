using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Update相关执行器
/// </summary>
public class MonoManager : SingleMonoBase<MonoManager>
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void AutoCreate()
    {
        if (INSTANCE != null)
            return;
        var go = new GameObject(nameof(MonoManager));
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<MonoManager>();
        DontDestroyOnLoad(go);
    }

    //Update任务集合
    public Action updateAction;
    //FixedUpdate任务集合
    public Action fixedUpdateAction;
    //LateUpdate任务集合
    public Action lateUpdateAction;


    /// <summary>
    /// ����Update����
    /// </summary>
    /// <param name="task">����</param>
    public void AddUpdateAction(Action task)
    {
        updateAction += task;
    }
    /// <summary>
    /// �Ƴ�Update����
    /// </summary>
    /// <param name="task">����</param>
    public void RemoveUpdateAction(Action task)
    {
        updateAction -= task;
    }

    /// <summary>
    /// ����FixedUpdate����
    /// </summary>
    /// <param name="task">����</param>
    public void AddFixedUpdateAction(Action task)
    {
        fixedUpdateAction += task;
    }
    /// <summary>
    /// �Ƴ�FixedUpdate����
    /// </summary>
    /// <param name="task">����</param>
    public void RemoveFixedUpdateAction(Action task)
    {
        fixedUpdateAction -= task;
    }

    /// <summary>
    /// 添加LateUpdate任务事件
    /// </summary>
    /// <param name="task">����</param>
    public void AddLateUpdateAction(Action task)
    {
        lateUpdateAction += task;
    }
    /// <summary>
    /// 移除LateUpdate任务事件
    /// </summary>
    /// <param name="task">任务</param>
    public void RemoveLateUpdateAction(Action task)
    {
        lateUpdateAction -= task;
    }


    void Update()
    {
        updateAction?.Invoke();
    }

    void FixedUpdate()
    {
        fixedUpdateAction?.Invoke();
    }

    private void LateUpdate()
    {
        lateUpdateAction?.Invoke();
    }
}

