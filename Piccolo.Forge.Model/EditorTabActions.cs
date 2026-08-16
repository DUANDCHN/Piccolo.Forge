namespace Piccolo.Forge.Model;

/// <summary>
/// 编辑区标签页右键菜单里的一项是什么。
/// </summary>
/// <remarks>
/// 与 <see cref="ToolWindowMenuItemKind"/> 同构：模型层只说"有哪些项、什么语义"，
/// 文案与控件由视图层拼。
/// </remarks>
public enum EditorTabMenuItemKind
{
    /// <summary>分隔线。</summary>
    Separator,

    /// <summary>关闭这一页。</summary>
    Close,

    /// <summary>关闭其它标签页。</summary>
    CloseOthers,

    /// <summary>关闭所有标签页。</summary>
    CloseAll,

    /// <summary>关闭左侧标签页。</summary>
    CloseToTheLeft,

    /// <summary>关闭右侧标签页。</summary>
    CloseToTheRight,

    /// <summary>向右拆分（新组在右，本页留在原组）。</summary>
    SplitRight,

    /// <summary>向下拆分。</summary>
    SplitDown,

    /// <summary>拆分并右移（本页搬去新组）。</summary>
    SplitAndMoveRight,

    /// <summary>拆分并下移。</summary>
    SplitAndMoveDown,

    /// <summary>固定 / 取消固定。</summary>
    TogglePinned,
}

/// <summary>
/// 编辑区标签页菜单里的一项。
/// </summary>
/// <param name="Kind">这一项是什么。</param>
public readonly record struct EditorTabMenuItem(EditorTabMenuItemKind Kind);

/// <summary>
/// 编辑区标签页的右键菜单：**只包含编辑区自己的语义**。
/// </summary>
/// <remarks>
/// 参照 Rider 的文档标签页右键菜单，但**有意只取其中属于编辑区的那部分**。
/// 上游那张菜单里还有「复制路径 / 引用」「重写文件类型」「重命名文件」「书签」
/// 「本地历史记录」「Git」「创建 Gist」「在新窗口中打开」等等 ——
/// 那些要么是 IDE 概念，要么依赖宿主的能力（版本控制、文件系统、书签库）。
/// <para>
/// <b>Forge 不认识这些，也不该认识。</b>宿主要往菜单里加自己的项，走
/// <c>ForgeEditorArea.TabMenuExtension</c> 那个口子追加，而不是让库去建模 Git。
/// </para>
/// <para>
/// 同样有意略过的还有「关闭未修改标签页」与「关闭所有只读项」——
/// "改没改过""只读不只读"是宿主对内容的判断，编辑区只知道 <c>IsCloseable</c> 和 <c>IsPinned</c>。
/// </para>
/// </remarks>
public static class EditorTabContextMenu
{
    /// <summary>标准菜单项，按显示次序。</summary>
    /// <returns>菜单项列表。</returns>
    public static IReadOnlyList<EditorTabMenuItem> Standard() =>
    [
        new(EditorTabMenuItemKind.Close),
        new(EditorTabMenuItemKind.CloseOthers),
        new(EditorTabMenuItemKind.CloseAll),
        new(EditorTabMenuItemKind.CloseToTheLeft),
        new(EditorTabMenuItemKind.CloseToTheRight),
        new(EditorTabMenuItemKind.Separator),
        new(EditorTabMenuItemKind.SplitRight),
        new(EditorTabMenuItemKind.SplitAndMoveRight),
        new(EditorTabMenuItemKind.SplitDown),
        new(EditorTabMenuItemKind.SplitAndMoveDown),
        new(EditorTabMenuItemKind.Separator),
        new(EditorTabMenuItemKind.TogglePinned),
    ];
}
