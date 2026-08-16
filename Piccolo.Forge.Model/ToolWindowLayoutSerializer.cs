using System.Globalization;
using System.Xml.Linq;

namespace Piccolo.Forge.Model;

/// <summary>
/// 布局的读写，XML 形状与属性名照抄上游。
/// </summary>
/// <remarks>
/// 标签与属性名出处见 <c>docs/upstream/工具窗口模型.md</c> §2：
/// 根标签 <c>layout</c>（<c>DesktopLayout.kt:25 · TAG</c>），
/// 每个窗口一个 <c>window_info</c>（<c>WindowInfoImpl.kt:34</c>），
/// 统一权重一个 <c>unified_weights</c>（<c>UnifiedToolWindowWeights.kt:17</c>）。
/// <para>
/// **只写与默认值不同的属性**：上游用 <c>BaseState</c> 的 property 委托，
/// 等于默认值就不进 XML。照抄这条，存档才不会一堆噪声。
/// </para>
/// </remarks>
public static class ToolWindowLayoutSerializer
{
    /// <summary>根标签（<c>DesktopLayout.kt:25</c>）。</summary>
    public const string LayoutTag = "layout";

    /// <summary>
    /// 「条带上显示工具窗口名字」写在 <see cref="LayoutTag"/> 上的属性名。
    /// </summary>
    /// <remarks>
    /// <b>有意与上游不同</b>：IntelliJ 把它放在全局的 <c>UISettings.showToolWindowsNames</c>，
    /// 而 Forge 不做全局设置存档 —— 它是「界面摆成什么样」的一部分，就跟布局一起存。
    /// 好处很实在：宿主的两条存档路径（自动存的「上次退出时的样子」与用户自己存的布局文件）
    /// **都写同一份 XML**，因此两条路自动都带上它，不必各自再开一个开关去记。
    /// </remarks>
    public const string ShowNamesAttribute = "show-names";

    /// <summary>单个窗口的标签（<c>WindowInfoImpl.kt:34</c>）。</summary>
    public const string WindowInfoTag = "window_info";

    /// <summary>统一权重的标签（<c>UnifiedToolWindowWeights.kt:17</c>）。</summary>
    public const string UnifiedWeightsTag = "unified_weights";

    /// <summary>
    /// 写成 XML。
    /// </summary>
    /// <remarks>条目按 <see cref="DesktopLayout.Comparer"/> 排序（<c>DesktopLayout.kt:153</c>）。</remarks>
    /// <param name="layout">布局。</param>
    /// <returns>根元素。</returns>
    /// <param name="showNames">条带上是否显示工具窗口名字（见 <see cref="ShowNamesAttribute"/>）。</param>
    public static XElement Write(DesktopLayout layout, bool showNames = false)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var root = new XElement(LayoutTag);
        root.SetAttributeValue(ShowNamesAttribute, showNames);

        foreach (var info in layout.GetSortedList())
        {
            root.Add(WriteWindowInfo(info));
        }

        var weights = new XElement(UnifiedWeightsTag);
        foreach (var anchor in new[]
                 {
                     ToolWindowAnchor.Top, ToolWindowAnchor.Left,
                     ToolWindowAnchor.Bottom, ToolWindowAnchor.Right,
                 })
        {
            weights.SetAttributeValue(anchor.ToXmlValue(), Float(layout.GetUnifiedAnchorWeight(anchor)));
        }

        root.Add(weights);
        return root;
    }

    /// <summary>
    /// 从 XML 读回来。
    /// </summary>
    /// <remarks>
    /// 读完按 <see cref="DesktopLayout.NormalizeOrder"/> 重排次序
    /// （<c>DesktopLayout.kt:127 · readExternal</c>），
    /// 没有 <c>id</c> 的条目直接丢掉（<c>:113-116</c>）。
    /// </remarks>
    /// <param name="root">根元素。</param>
    /// <returns>读出来的布局。</returns>
    public static DesktopLayout Read(XElement root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var infos = new List<WindowInfo>();
        foreach (var element in root.Elements(WindowInfoTag))
        {
            var id = (string?)element.Attribute("id");
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            infos.Add(ReadWindowInfo(element, id));
        }

        var layout = new DesktopLayout(infos);

        if (root.Element(UnifiedWeightsTag) is { } weights)
        {
            foreach (var anchor in Enum.GetValues<ToolWindowAnchor>())
            {
                if (weights.Attribute(anchor.ToXmlValue()) is { } attribute
                    && float.TryParse(attribute.Value, CultureInfo.InvariantCulture, out var value))
                {
                    layout.SetUnifiedAnchorWeight(anchor, value);
                }
            }
        }

        layout.NormalizeOrder();
        return layout;
    }

    private static XElement WriteWindowInfo(WindowInfo info)
    {
        var element = new XElement(WindowInfoTag);
        element.SetAttributeValue("id", info.Id);

        // 只写与默认值不同的
        Set(element, "anchor", info.Anchor != ToolWindowAnchor.Left, () => info.Anchor.ToXmlValue());
        Set(element, "side_tool", info.IsSplit, () => "true");
        Set(element, "type", info.Type != ToolWindowType.Docked, () => info.Type.ToString().ToUpperInvariant());
        Set(element, "internal_type", info.InternalType != ToolWindowType.Docked,
            () => info.InternalType.ToString().ToUpperInvariant());
        Set(element, "content_ui", info.ContentUiType != ToolWindowContentUiType.Tabbed, () => "combo");
        Set(element, "visible", info.IsVisible, () => "true");
        Set(element, "active", info.IsActiveOnStart, () => "true");
        Set(element, "auto_hide", info.IsAutoHide, () => "true");
        Set(element, "maximized", info.IsMaximized, () => "true");
        Set(element, "show_stripe_button", !info.IsShowStripeButton, () => "false");
        Set(element, "weight", Math.Abs(info.Weight - WindowInfo.DefaultWeight) > float.Epsilon,
            () => Float(info.Weight));
        Set(element, "sideWeight", Math.Abs(info.SideWeight - 0.5f) > float.Epsilon, () => Float(info.SideWeight));
        Set(element, "order", info.Order != -1, () => info.Order.ToString(CultureInfo.InvariantCulture));
        Set(element, "toolWindowPaneId",
            !string.Equals(info.PaneId, WindowInfo.DefaultPaneId, StringComparison.Ordinal), () => info.PaneId);

        // floatingBounds 是扁平展开的四个属性（WindowInfoImpl.kt:52-53）
        if (info.FloatingBounds is { } bounds)
        {
            element.SetAttributeValue("x", bounds.X);
            element.SetAttributeValue("y", bounds.Y);
            element.SetAttributeValue("width", bounds.Width);
            element.SetAttributeValue("height", bounds.Height);
        }

        return element;
    }

    private static WindowInfo ReadWindowInfo(XElement element, string id)
    {
        var info = new WindowInfo
        {
            Id = id,
            PaneId = (string?)element.Attribute("toolWindowPaneId") ?? WindowInfo.DefaultPaneId,
            Anchor = ToolWindowAnchorExtensions.ParseAnchorOrLeft((string?)element.Attribute("anchor") ?? "left"),
            IsSplit = Bool(element, "side_tool", false),
            Type = ParseType(element, "type"),
            InternalType = ParseType(element, "internal_type"),
            ContentUiType = string.Equals((string?)element.Attribute("content_ui"), "combo", StringComparison.Ordinal)
                ? ToolWindowContentUiType.Combo
                : ToolWindowContentUiType.Tabbed,
            IsVisible = Bool(element, "visible", false),
            IsActiveOnStart = Bool(element, "active", false),
            IsAutoHide = Bool(element, "auto_hide", false),
            IsMaximized = Bool(element, "maximized", false),
            IsShowStripeButton = Bool(element, "show_stripe_button", true),
            Weight = Single(element, "weight", WindowInfo.DefaultWeight),
            SideWeight = Single(element, "sideWeight", 0.5f),
            Order = (int)Single(element, "order", -1),
            FloatingBounds = ReadBounds(element),
        };

        return info;
    }

    private static FloatingBounds? ReadBounds(XElement element)
    {
        if (element.Attribute("width") is null)
        {
            return null;
        }

        var bounds = new FloatingBounds(
            (int)Single(element, "x", 0),
            (int)Single(element, "y", 0),
            (int)Single(element, "width", 0),
            (int)Single(element, "height", 0));

        // 全零等于未定义（WindowInfoImpl.kt:53）
        return bounds is { X: 0, Y: 0, Width: 0, Height: 0 } ? null : bounds;
    }

    private static ToolWindowType ParseType(XElement element, string name)
        => Enum.TryParse<ToolWindowType>((string?)element.Attribute(name), ignoreCase: true, out var value)
            ? value
            : ToolWindowType.Docked;

    /// <summary>读回「条带上显示工具窗口名字」。</summary>
    /// <param name="root">存档根元素。</param>
    /// <param name="fallback">存档里没写时用什么。</param>
    /// <returns>存档里的值。</returns>
    public static bool ReadShowNames(XElement root, bool fallback = false)
    {
        ArgumentNullException.ThrowIfNull(root);
        return Bool(root, ShowNamesAttribute, fallback);
    }

    private static bool Bool(XElement element, string name, bool fallback)
        => bool.TryParse((string?)element.Attribute(name), out var value) ? value : fallback;

    private static float Single(XElement element, string name, float fallback)
        => float.TryParse((string?)element.Attribute(name), CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private static void Set(XElement element, string name, bool condition, Func<string> value)
    {
        if (condition)
        {
            element.SetAttributeValue(name, value());
        }
    }

    private static string Float(float value) => value.ToString("R", CultureInfo.InvariantCulture);
}
