// Adapted from Dock.Model DockManager/Factory move semantics (MIT),
// commit 3f2473fae54cf3c9b5079bd56c3612334a88f6ca.
// Copyright (c) Wiesław Šoltés. See THIRD-PARTY-NOTICES.md.

namespace Piccolo.Forge.Model;

/// <summary>标签页在组与组之间搬移的唯一写入口。</summary>
/// <remarks>
/// 与 Dock 的 <c>Factory.MoveDockable</c> 同构：先从 source detach，再向 target attach，
/// 最后一次性提交布局通知。关闭标签页是另一条生命周期，搬移绝不触发关闭事件。
/// <para>
/// 借的是**事务顺序**，不是它的词汇：落点方向直接用 Forge 自己的
/// <see cref="EditorDropSide"/>（拖放策略 <see cref="EditorDropPolicy"/> 用的也是它）。
/// 曾经这里另有一套 <c>DocumentDockOperation</c>，两套枚举一一对应、
/// 调用方还得写个 switch 翻译一遍 —— 同一件事建两次模型，删了。
/// </para>
/// </remarks>
internal static class DocumentDockFactory
{
    public static bool MoveDockable(
        string tabId,
        EditorGroup source,
        EditorGroup target,
        EditorDropSide side,
        Action<EditorNode, EditorNode> replace,
        Action<EditorGroup> setActiveDock,
        Action commit)
    {
        var moving = source.Tabs.FirstOrDefault(x => x.Id == tabId);
        if (moving.Id is null || side == EditorDropSide.None)
        {
            return false;
        }

        if (side == EditorDropSide.Center)
        {
            if (ReferenceEquals(source, target))
            {
                return false;
            }

            // Dock Factory 顺序不可反：目标 TabStrip 可能要收养宿主 Control 图标。
            source.Close(tabId);
            if (!target.Tabs.Any(x => x.Id == tabId))
            {
                target.Add(moving);
            }
            else
            {
                target.Select(tabId);
            }

            setActiveDock(target);
            commit();
            return true;
        }

        if (ReferenceEquals(source, target) && source.Tabs.Count <= 1)
        {
            return false;
        }

        var created = new EditorGroup();
        created.Add(moving);
        var vertical = side is EditorDropSide.Top or EditorDropSide.Bottom;
        var second = side is EditorDropSide.Right or EditorDropSide.Bottom;
        replace(target, second ? new EditorSplit(target, created, vertical) : new EditorSplit(created, target, vertical));
        source.Close(tabId);
        setActiveDock(created);
        commit();
        return true;
    }
}
