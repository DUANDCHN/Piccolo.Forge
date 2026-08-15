using Avalonia.Media;
using Avalonia.Styling;

namespace Piccolo.Forge.Themes;

/// <summary>
/// 一套配色。取值来自上游主题文件，逐条出处见 <c>docs/upstream/度量与主题键.md</c> §6。
/// </summary>
/// <remarks>
/// 「—（该主题没显式写）」的键按上游的解析链补齐：
/// `*` 通配 → 父主题 → 代码兜底（§7）。补的时候用哪一条，下面每个属性的注释都写了。
/// </remarks>
public sealed record ForgePalette
{
    /// <summary>工具窗口面板背景（`ToolWindow.background`）。</summary>
    public required Color Background { get; init; }

    /// <summary>条带背景（`ToolWindow.Stripe.background`；两份主题都没写，回落 <see cref="Background"/>）。</summary>
    public required Color StripeBackground { get; init; }

    /// <summary>条带边框（`ToolWindow.Stripe.borderColor`；没写，回落 `Borders.color`）。</summary>
    public required Color StripeBorder { get; init; }

    /// <summary>工具窗口整体边框（`ToolWindow.borderColor`；没写，回落 `Borders.color`）。</summary>
    public required Color WindowBorder { get; init; }

    /// <summary>条带上的分组分隔线（`ToolWindow.Stripe.separatorColor`）。</summary>
    public required Color StripeSeparator { get; init; }

    /// <summary>拖拽中的分组分隔线（`ToolWindow.Stripe.DragAndDrop.separatorColor`）。</summary>
    public required Color StripeSeparatorDragAndDrop { get; init; }

    /// <summary>条带按钮前景（`ToolWindow.Button.foreground`）。</summary>
    public required Color ButtonForeground { get; init; }

    /// <summary>条带按钮选中前景（`ToolWindow.Button.selectedForeground`）。</summary>
    public required Color ButtonSelectedForeground { get; init; }

    /// <summary>条带按钮选中背景（`ToolWindow.Button.selectedBackground`）。</summary>
    public required Color ButtonSelectedBackground { get; init; }

    /// <summary>
    /// 「对应窗口可见」时的按钮背景（`ActionButton.pressedBackground`，带 alpha）。
    /// </summary>
    /// <remarks>优先级见 <c>SquareStripeButton.kt:208-219 · getBackgroundColor</c>。</remarks>
    public required Color ButtonPressedBackground { get; init; }

    /// <summary>悬停时的按钮背景（`ActionButton.hoverBackground`，带 alpha）。</summary>
    public required Color ButtonHoverBackground { get; init; }

    /// <summary>
    /// 拖拽时条带上那个落点方块的填充（`ToolWindow.Button.DragAndDrop.buttonDropBackground`）。
    /// </summary>
    /// <remarks>
    /// 画法见 <c>AbstractDroppableStripe.kt:634-642</c>：去掉图标 padding、
    /// 圆角取 <c>Toolbar.stripeButtonArc</c>，填这个色。
    /// </remarks>
    public required Color ButtonDropBackground { get; init; }

    /// <summary>
    /// 拖到「另一半」时那个虚线方框的颜色（`ToolWindow.Button.DragAndDrop.buttonDropBorderColor`）。
    /// </summary>
    /// <remarks>
    /// <c>AbstractDroppableStripe.kt:644-654</c>：只在竖条带、且分隔器可见时画，
    /// 边长 30、虚线 <c>BasicStroke(1, …, dash = [4,4])</c>，画在分隔器的上边或下边。
    /// </remarks>
    public required Color ButtonDropBorder { get; init; }

    /// <summary>
    /// 拖拽时目标停靠区的整片高亮（`ToolWindow.DragAndDrop.areaBackground`，带 alpha）。
    /// </summary>
    /// <remarks>
    /// <c>ToolWindowDragHelper.kt:126-134 · createDropTargetHighlightComponent</c>：
    /// 新 UI 用这个键，老 UI 用 <c>DragAndDrop.Area.BACKGROUND</c>。
    /// </remarks>
    public required Color DragAreaBackground { get; init; }

    /// <summary>
    /// 标题条背景（`ToolWindow.Header.inactiveBackground`）。
    /// </summary>
    /// <remarks>
    /// <b>新 UI 只有这一种</b>：<c>ToolWindowHeader.kt:312</c> 是
    /// <c>val active = !isNewUi &amp;&amp; isActive</c> —— 新 UI 下 <c>active</c> 恒为 <c>false</c>，
    /// 于是 <c>UIUtil.drawHeader</c> 永远走 <c>JBUI.ToolWindow.headerBackground()</c>，
    /// 也就是这个键。
    /// <para>
    /// 那条「激活标题条背景」（<c>ToolWindow.Header.background</c>，
    /// <c>JBUI.java:1166-1168 · headerActiveBackground</c>）**expUI 根本没定义**，
    /// 回落到父主题的 <c>0xE2E6EC</c>/Darcula 深蓝灰 —— 照它画的结果是
    /// 活动面板顶上糊一条老 UI 配色的高亮带，一眼就不像。所以这里<b>不留</b>那个字段：
    /// 留着就会有人接着用。
    /// </para>
    /// </remarks>
    public required Color HeaderBackground { get; init; }

    /// <summary>标题条边框（`ToolWindow.Header.borderColor`；没写，回落 `DefaultTabs.borderColor` → `Borders.color`）。</summary>
    public required Color HeaderBorder { get; init; }

    /// <summary>标签页悬停背景（`ToolWindow.HeaderTab.hoverBackground`）。</summary>
    public required Color HeaderTabHoverBackground { get; init; }

    /// <summary>选中标签页的下划线（`ToolWindow.HeaderTab.underlineColor`；没写，回落 `*.underlineColor`）。</summary>
    public required Color HeaderTabUnderline { get; init; }

    /// <summary>失焦时选中标签页的下划线（`ToolWindow.HeaderTab.inactiveUnderlineColor`；回落 `*.inactiveUnderlineColor`）。</summary>
    public required Color HeaderTabInactiveUnderline { get; init; }

    /// <summary>常规文字色（`*.foreground`）。</summary>
    public required Color Foreground { get; init; }

    /// <summary>
    /// 按宿主当前的明暗变体挑一套。
    /// </summary>
    /// <remarks>
    /// 宿主的明暗通常是跟随系统的；钉死一套会出现「软件其余部分变白、外壳还是黑的」那道硬边。
    /// 未知变体按浅色处理，与 Avalonia 的默认一致。
    /// </remarks>
    /// <param name="variant">明暗变体。</param>
    /// <returns>该用的配色。</returns>
    public static ForgePalette ForVariant(ThemeVariant? variant)
        => variant == ThemeVariant.Dark ? ExpUiDark : ExpUiLight;

    /// <summary>expUI Light。</summary>
    public static ForgePalette ExpUiLight { get; } = new()
    {
        Background = ForgeColor.Parse("#F7F8FA"),                       // Gray13
        StripeBackground = ForgeColor.Parse("#F7F8FA"),                 // 回落 ToolWindow.background
        StripeBorder = ForgeColor.Parse("#EBECF0"),                     // Borders.color / Gray12
        WindowBorder = ForgeColor.Parse("#EBECF0"),
        StripeSeparator = ForgeColor.Parse("#C9CCD6"),                  // Gray9
        StripeSeparatorDragAndDrop = ForgeColor.Parse("#709CF5"),       // Blue7
        ButtonForeground = ForgeColor.Parse("#5A5D6B"),                 // Gray5
        ButtonSelectedForeground = ForgeColor.Parse("#FFFFFF"),         // Gray14
        ButtonSelectedBackground = ForgeColor.Parse("#3574F0"),         // Blue4
        ButtonPressedBackground = ForgeColor.Parse("#0000001D"),        // ActionButton.pressedBackground
        ButtonHoverBackground = ForgeColor.Parse("#00000012"),          // ActionButton.hoverBackground
        ButtonDropBackground = ForgeColor.Parse("#D4E2FF"),             // Blue11
        ButtonDropBorder = ForgeColor.Parse("#709CF5"),                 // Blue7
        DragAreaBackground = ForgeColor.Parse("#A0BDF84D"),             // 带 alpha
        HeaderBackground = ForgeColor.Parse("#F7F8FA"),                 // Gray13
        HeaderBorder = ForgeColor.Parse("#EBECF0"),
        HeaderTabHoverBackground = ForgeColor.Parse("#EBECF0"),         // Gray12
        HeaderTabUnderline = ForgeColor.Parse("#3574F0"),               // *.underlineColor / Blue4
        HeaderTabInactiveUnderline = ForgeColor.Parse("#A8ADBD"),       // *.inactiveUnderlineColor / Gray8
        Foreground = ForgeColor.Parse("#000000"),                       // 父主题 IntelliJ 的 foreground
    };

    /// <summary>expUI Dark。</summary>
    public static ForgePalette ExpUiDark { get; } = new()
    {
        Background = ForgeColor.Parse("#2B2D30"),                       // *.background / Gray2
        StripeBackground = ForgeColor.Parse("#2B2D30"),
        StripeBorder = ForgeColor.Parse("#1E1F22"),                     // Borders.color / Gray1
        WindowBorder = ForgeColor.Parse("#1E1F22"),
        StripeSeparator = ForgeColor.Parse("#43454A"),                  // Gray4
        StripeSeparatorDragAndDrop = ForgeColor.Parse("#548AF7"),       // Blue8
        ButtonForeground = ForgeColor.Parse("#9DA0A8"),                 // Gray9
        ButtonSelectedForeground = ForgeColor.Parse("#FFFFFF"),         // Gray14
        ButtonSelectedBackground = ForgeColor.Parse("#3574F0"),         // Blue6
        ButtonPressedBackground = ForgeColor.Parse("#FFFFFF26"),
        ButtonHoverBackground = ForgeColor.Parse("#FFFFFF16"),
        ButtonDropBackground = ForgeColor.Parse("#35538F99"),           // 带 alpha
        ButtonDropBorder = ForgeColor.Parse("#548AF7"),                 // Blue8
        DragAreaBackground = ForgeColor.Parse("#366ACF4D"),             // 带 alpha
        HeaderBackground = ForgeColor.Parse("#2B2D30"),                 // Gray2
        HeaderBorder = ForgeColor.Parse("#1E1F22"),
        HeaderTabHoverBackground = ForgeColor.Parse("#393B40"),         // *.hoverBackground / Gray3
        HeaderTabUnderline = ForgeColor.Parse("#3574F0"),               // *.underlineColor / Blue6
        HeaderTabInactiveUnderline = ForgeColor.Parse("#5A5D63"),       // *.inactiveUnderlineColor / Gray6
        Foreground = ForgeColor.Parse("#DFE1E5"),                       // *.foreground / Gray12
    };
}

/// <summary>
/// 颜色字面量的解析。
/// </summary>
/// <remarks>
/// **上游的 8 位十六进制是 <c>#RRGGBBAA</c>，alpha 在最后**，
/// 与 Avalonia / WPF 的 <c>#AARRGGBB</c> 相反。
/// 证据：`ToolWindow.DragAndDrop.areaBackground = #A0BDF84D`，
/// 而代码兜底是 <c>new JBColor(0xA0BDF8, 0x366ACF)</c>
/// （<c>JBUI.java:1141-1142</c>）—— 前 6 位是 RGB，末 2 位是 alpha。
/// 抄主题值时必须换位，否则整片颜色都会错。
/// </remarks>
public static class ForgeColor
{
    /// <summary>
    /// 按上游的写法解析颜色：<c>#RRGGBB</c> 或 <c>#RRGGBBAA</c>。
    /// </summary>
    /// <param name="value">上游主题里的颜色字面量。</param>
    /// <returns>解析出的颜色。</returns>
    /// <exception cref="FormatException">长度不是 6 或 8 位。</exception>
    public static Color Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var hex = value.StartsWith('#') ? value[1..] : value;

        return hex.Length switch
        {
            6 => Color.FromRgb(Byte(hex, 0), Byte(hex, 2), Byte(hex, 4)),
            8 => Color.FromArgb(Byte(hex, 6), Byte(hex, 0), Byte(hex, 2), Byte(hex, 4)),
            _ => throw new FormatException($"上游颜色应当是 6 或 8 位十六进制（#RRGGBB[AA]），拿到的是 '{value}'"),
        };
    }

    private static byte Byte(string hex, int offset)
        => Convert.ToByte(hex.Substring(offset, 2), 16);
}
