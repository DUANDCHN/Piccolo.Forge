namespace Piccolo.Forge.Model;

/// <summary>
/// 工具窗口的形态。
/// </summary>
/// <remarks>对应 <c>platform/ide-core/src/com/intellij/openapi/wm/ToolWindowType.java:4</c>。</remarks>
public enum ToolWindowType
{
    /// <summary>停靠，挤占编辑区。上游默认值。</summary>
    Docked,

    /// <summary>浮动窗口。</summary>
    Floating,

    /// <summary>滑出，盖在编辑区上。</summary>
    Sliding,

    /// <summary>独立系统窗口。</summary>
    Windowed,
}

/// <summary>
/// 工具窗口内容区的呈现方式。
/// </summary>
/// <remarks>
/// 对应 <c>platform/ide-core/src/com/intellij/openapi/wm/ToolWindowContentUiType.java:11-12</c>。
/// </remarks>
public enum ToolWindowContentUiType
{
    /// <summary>标签页。上游默认值，持久化写 <c>tabs</c>。</summary>
    Tabbed,

    /// <summary>下拉框。持久化写 <c>combo</c>。</summary>
    Combo,
}
