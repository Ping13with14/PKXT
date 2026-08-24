using UnityEngine;

/// <summary>
/// 玩家移动策略抽象基类。
/// 开放-封闭原则：移动体系可扩展、不可修改——
/// 新增移动方式只需新建 PlayerMoveStrategy 子类并赋值给 PlayerController.moveStrategy，
/// 无需改动任何现有状态/控制器代码。
/// </summary>
public abstract class PlayerMoveStrategy
{
    /// <summary>宿主控制器</summary>
    protected PlayerController controller;
    /// <summary>角色模型</summary>
    protected PlayerModel model;

    /// <summary>
    /// 绑定宿主引用。由 PlayerController.Awake 或外部切换策略时调用。
    /// </summary>
    public virtual void Init(PlayerController controller)
    {
        this.controller = controller;
        this.model = controller != null ? controller.playerModel : null;
    }

    /// <summary>
    /// 执行移动：读取 controller.inputMoveVec2 → 写入 controller.playerRigidbody 水平速度。
    /// 由各移动状态每帧调用（经由 PlayerController.MoveByInput 委托）。
    /// </summary>
    public abstract void Move(float speed);
}
