using UnityEngine;

/// <summary>
/// 单例模式改造器
/// </summary>
/// <typeparam name="T"></typeparam>
public class SingleMonoBase<T> : MonoBehaviour where T : SingleMonoBase<T>
{
    //子类的单例
    public static T INSTANCE;

    protected virtual void Awake()
    {
        if (INSTANCE != null)
            Debug.LogError(this + "不符合单例模式");
        INSTANCE = (T) this;
    }

    protected virtual void OnDestroy()
    {
        Destroy();
    }

    /// <summary>
    /// 清除子类单例
    /// </summary>
    public void Destroy()
    {
        INSTANCE = null;
    }
}
