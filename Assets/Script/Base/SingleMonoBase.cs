using UnityEngine;

/// <summary>
/// 单例基类：场景中只允许存在一个 T 实例。
/// 若检测到重复实例，保留先创建的实例并销毁后来者。
/// </summary>
/// <typeparam name="T"></typeparam>
public class SingleMonoBase<T> : MonoBehaviour where T : SingleMonoBase<T>
{
    //子类的单例
    public static T INSTANCE;

    protected virtual void Awake()
    {
        if (INSTANCE != null)
        {
            Debug.LogError($"{this} 不符合单例模式：已存在实例 {INSTANCE}，销毁当前重复实例");
            Destroy(gameObject);
            return;
        }
        INSTANCE = (T)this;
    }

    protected virtual void OnDestroy()
    {
        // 仅当销毁的是当前单例时才清空引用，避免误清后续实例
        if (ReferenceEquals(INSTANCE, this))
            INSTANCE = null;
    }
}
