namespace Piccolo.Forge.Model;

/// <summary>
/// 工具窗口停靠的边。
/// </summary>
/// <remarks>
/// 对应上游 <c>platform/ide-core/src/com/intellij/openapi/wm/ToolWindowAnchor.java:16-21</c>。
/// 上游是不可扩展的常量类，只有这四个值。
/// </remarks>
public enum ToolWindowAnchor
{
    /// <summary>上边。</summary>
    Top,

    /// <summary>左边。上游的默认值。</summary>
    Left,

    /// <summary>下边。</summary>
    Bottom,

    /// <summary>右边。</summary>
    Right,
}

/// <summary>
/// <see cref="ToolWindowAnchor"/> 的派生量。
/// </summary>
public static class ToolWindowAnchorExtensions
{
    /// <summary>
    /// 是否是横边（上/下）。
    /// </summary>
    /// <remarks>对应 <c>ToolWindowAnchor.java:49-51 · isHorizontal</c>。</remarks>
    /// <param name="anchor">停靠边。</param>
    /// <returns>上或下时为 <see langword="true"/>。</returns>
    public static bool IsHorizontal(this ToolWindowAnchor anchor)
        => anchor is ToolWindowAnchor.Top or ToolWindowAnchor.Bottom;

    /// <summary>
    /// 排序用的权重：TOP=1、LEFT=2、BOTTOM=3、RIGHT=4。
    /// </summary>
    /// <remarks>
    /// 对应 <c>platform/platform-impl/src/com/intellij/openapi/wm/impl/DesktopLayout.kt:180-188 · getAnchorWeight</c>。
    /// 只用于排序与序列化次序，与视觉位置无关。
    /// </remarks>
    /// <param name="anchor">停靠边。</param>
    /// <returns>1 到 4 的权重。</returns>
    public static int SortWeight(this ToolWindowAnchor anchor) => anchor switch
    {
        ToolWindowAnchor.Top => 1,
        ToolWindowAnchor.Left => 2,
        ToolWindowAnchor.Bottom => 3,
        ToolWindowAnchor.Right => 4,
        _ => 0,
    };

    /// <summary>
    /// 持久化用的字符串。
    /// </summary>
    /// <remarks>对应 <c>ToolWindowAnchor.java:36-38 · toString</c>。</remarks>
    /// <param name="anchor">停靠边。</param>
    /// <returns><c>top</c> / <c>left</c> / <c>bottom</c> / <c>right</c>。</returns>
    public static string ToXmlValue(this ToolWindowAnchor anchor) => anchor switch
    {
        ToolWindowAnchor.Top => "top",
        ToolWindowAnchor.Left => "left",
        ToolWindowAnchor.Bottom => "bottom",
        ToolWindowAnchor.Right => "right",
        _ => throw new ArgumentOutOfRangeException(nameof(anchor)),
    };

    /// <summary>
    /// 从持久化字符串解析；无法识别时回落 <see cref="ToolWindowAnchor.Left"/>。
    /// </summary>
    /// <remarks>
    /// 回落行为对应 <c>WindowInfoImpl.kt:149-163 · ToolWindowAnchorConverter</c>：
    /// 上游解析失败也是回落 LEFT，只是会打一条 warn（<c>"none"</c> 除外）。
    /// </remarks>
    /// <param name="value">持久化字符串。</param>
    /// <returns>解析出的停靠边。</returns>
    public static ToolWindowAnchor ParseAnchorOrLeft(string? value) => value switch
    {
        "top" => ToolWindowAnchor.Top,
        "left" => ToolWindowAnchor.Left,
        "bottom" => ToolWindowAnchor.Bottom,
        "right" => ToolWindowAnchor.Right,
        _ => ToolWindowAnchor.Left,
    };
}
