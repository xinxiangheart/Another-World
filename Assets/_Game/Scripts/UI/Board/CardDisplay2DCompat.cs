using UnityEngine;

/// <summary>
/// 兼容垫片：让新预制体（含 CardDisplay2DNew）能响应旧代码的 GetComponent&lt;CardDisplay2D&gt;() 调用。
/// 继承 CardDisplay2D 但【不新增任何序列化字段】——避免 Unity 父子类字段名重复序列化冲突
/// （CardDisplay2DNew 的 nameText/costText 等与基类同名会触发 "not supported" 错误）。
/// 把 Refresh/RefreshWithInstance 委托给同物体上的 CardDisplay2DNew，实现新显示逻辑。
/// </summary>
public class CardDisplay2DCompat : CardDisplay2D
{
    CardDisplay2DNew _new;

    public override void RefreshWithInstance(CardInstance inst)
    {
        EnsureRef();
        if (_new != null) _new.RefreshWithInstance(inst);
        else base.RefreshWithInstance(inst);
    }

    public override void Refresh()
    {
        EnsureRef();
        if (_new != null) _new.Refresh();
        else base.Refresh();
    }

    void EnsureRef()
    {
        if (_new == null) _new = GetComponent<CardDisplay2DNew>();
    }
}
