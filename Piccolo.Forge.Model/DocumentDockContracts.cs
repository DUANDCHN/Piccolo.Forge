// Adapted from Dock.Model (MIT), commit 3f2473fae54cf3c9b5079bd56c3612334a88f6ca.
// Copyright (c) Wiesław Šoltés. See THIRD-PARTY-NOTICES.md.

namespace Piccolo.Forge.Model;

/// <summary>可放进文档停靠区的一个文档。</summary>
/// <remarks>
/// 对应 Dock.Model.Core.IDockable。Forge 只搬文档区实际需要的契约，
/// 不把浮窗、工具窗口、序列化等无关能力伪装成已实现。
/// </remarks>
public interface IDockable
{
    /// <summary>稳定身份。</summary>
    string Id { get; }

    /// <summary>显示标题。</summary>
    string Title { get; }
}

/// <summary>一组共享标签条和正文槽的文档。</summary>
/// <remarks>对应 Dock.Model.Controls.IDocumentDock。</remarks>
public interface IDocumentDock
{
    /// <summary>当前组里的文档顺序。</summary>
    IReadOnlyList<EditorTab> VisibleDockables { get; }

    /// <summary>活动文档。</summary>
    string? ActiveDockableId { get; }
}

/// <summary>文档停靠操作。</summary>
public enum DocumentDockOperation
{
    /// <summary>无操作。</summary>
    None,
    /// <summary>并入目标标签条。</summary>
    Fill,
    /// <summary>放在目标左侧。</summary>
    Left,
    /// <summary>放在目标右侧。</summary>
    Right,
    /// <summary>放在目标上侧。</summary>
    Top,
    /// <summary>放在目标下侧。</summary>
    Bottom,
}
