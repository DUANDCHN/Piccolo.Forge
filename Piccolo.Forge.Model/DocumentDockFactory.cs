// Adapted from Dock.Model DockManager/Factory move semantics (MIT),
// commit 3f2473fae54cf3c9b5079bd56c3612334a88f6ca.
// Copyright (c) Wiesław Šoltés. See THIRD-PARTY-NOTICES.md.

namespace Piccolo.Forge.Model;

/// <summary>DocumentDock 间文档搬移的唯一写入口。</summary>
/// <remarks>
/// 与 Dock 的 Factory.MoveDockable 同构：先从 source detach，再向 target attach，
/// 最后一次性提交布局通知。关闭文档是另一条生命周期，搬移绝不触发关闭事件。
/// </remarks>
internal static class DocumentDockFactory
{
    public static bool MoveDockable(
        string dockableId,
        EditorGroup source,
        EditorGroup target,
        DocumentDockOperation operation,
        Action<EditorNode, EditorNode> replace,
        Action<EditorGroup> setActiveDock,
        Action commit)
    {
        var dockable = source.Tabs.FirstOrDefault(x => x.Id == dockableId);
        if (dockable.Id is null || operation == DocumentDockOperation.None)
        {
            return false;
        }

        if (operation == DocumentDockOperation.Fill)
        {
            if (ReferenceEquals(source, target))
            {
                return false;
            }

            // Dock Factory 顺序不可反：目标 TabStrip 可能要收养宿主 Control 图标。
            source.Close(dockableId);
            if (!target.Tabs.Any(x => x.Id == dockableId))
            {
                target.Add(dockable);
            }
            else
            {
                target.Select(dockableId);
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
        created.Add(dockable);
        var vertical = operation is DocumentDockOperation.Top or DocumentDockOperation.Bottom;
        var second = operation is DocumentDockOperation.Right or DocumentDockOperation.Bottom;
        replace(target, second ? new EditorSplit(target, created, vertical) : new EditorSplit(created, target, vertical));
        source.Close(dockableId);
        setActiveDock(created);
        commit();
        return true;
    }
}
