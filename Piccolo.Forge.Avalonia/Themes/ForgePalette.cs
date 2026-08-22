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
    /// <summary>
    /// 这套配色属于哪个族。
    /// </summary>
    /// <remarks>
    /// 族除了决定颜色，还决定一批**度量**（见 <see cref="ForgeStripeButtonMetrics.ForTheme"/>）。
    /// 显式带着它，控件就不必让宿主再传一遍族——控件手里有 <see cref="ForgePalette"/> 就够了。
    /// </remarks>
    public ForgeThemeFamily Family { get; init; }

    /// <summary>工具窗口面板背景（`ToolWindow.background`）。岛形态下这是**岛**的颜色。</summary>
    public required Color Background { get; init; }

    /// <summary>
    /// 主窗口背景（`MainWindow.background`）。岛形态下这是**海**——岛之间那条缝露出来的就是它。
    /// </summary>
    /// <remarks>
    /// expUI 没有「海」这个概念（面板彼此贴死），两套 expUI 配色里它等于 <see cref="Background"/>，
    /// 于是缝宽为 0 时退化成原来的样子。出处见 <c>docs/upstream/Islands主题.md</c> §3。
    /// </remarks>
    public required Color MainBackground { get; init; }

    /// <summary>
    /// 岛的描边色（`Island.borderColor`）。
    /// </summary>
    /// <remarks>
    /// 上游把它定成 `tool-window-bg`，**与岛自己同色**——它不是一条看得见的框线，
    /// 而是把抗锯齿后的圆角边缘补实。照抄，别改成对比色。
    /// </remarks>
    public required Color IslandBorder { get; init; }

    /// <summary>
    /// 岛的几何；<see langword="null"/> 表示这套配色**不是岛形态**（expUI：方角、面板贴死）。
    /// </summary>
    /// <remarks>
    /// 判「是不是岛」一律看这里是否为 <see langword="null"/>，不要拿
    /// <see cref="MainBackground"/> 与 <see cref="Background"/> 是否相等去猜——
    /// 将来完全可能有一套「岛与海同色、只靠描边区分」的主题。
    /// </remarks>
    public ForgeIslandMetrics? Island { get; init; }

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
    /// 编辑区**选中**标签页的底色（`EditorTabs.underlinedTabBackground`）。
    /// </summary>
    /// <remarks>
    /// ⚠ 编辑区的标签页有**自己一族** `EditorTabs.*` 键，与工具窗口标题条的
    /// `ToolWindow.HeaderTab.*` 不是一回事。早先编辑区借用了后者，于是选中态只剩一条下划线——
    /// 因为 expUI 下这个键等于编辑区底色（Dark 都是 `Gray1`、Light 都是 `Gray14`），
    /// 借来的那套里根本没有"选中底"这个概念，看不出问题；
    /// 换到 Islands 才暴露：它这里是**蓝色调的实底**（blue-40 / blue-150）。
    /// </remarks>
    public required Color EditorTabSelectedBackground { get; init; }

    /// <summary>
    /// 编辑区选中标签页的描边（`EditorTabs.underlinedBorderColor`）；expUI 没定义，取透明。
    /// </summary>
    public required Color EditorTabSelectedBorder { get; init; }

    /// <summary>
    /// 编辑区标签页的悬停底（`EditorTabs.hoverBackground`）。
    /// </summary>
    /// <remarks>
    /// ⚠ **expUI 下它是全透明的**（`#1E1F2200` / `#FFFFFF00`——末两位是 alpha，见
    /// <see cref="ForgeColor"/>）。那不是写错，是 expUI 的编辑区标签页**本来就没有悬停底**。
    /// 别"顺手补一个能看见的颜色"。
    /// </remarks>
    public required Color EditorTabHoverBackground { get; init; }

    /// <summary>
    /// 编辑区**失焦时**选中标签页的底（`EditorTabs.inactiveUnderlinedTabBackground`）。
    /// </summary>
    /// <remarks>
    /// 上游的标签页有**五种状态**（<c>IslandsTabPainter.kt:254-268 · getColors</c>）：
    /// 常规 / 悬停 / 选中+活动 / 选中+失焦 / 选中+失焦+悬停。
    /// 早先只做了「选中」与「悬停」两种，失焦时仍画成活动色 —— 分屏之后两组都亮着，
    /// 看不出焦点在哪。
    /// </remarks>
    public required Color EditorTabInactiveSelectedBackground { get; init; }

    /// <summary>编辑区失焦时选中标签页的描边（`EditorTabs.inactiveUnderlinedTabBorderColor`）。</summary>
    public required Color EditorTabInactiveSelectedBorder { get; init; }

    /// <summary>
    /// 标签条**下面那条分割线**（`EditorTabs.underTabsBorderColor`）。
    /// </summary>
    /// <remarks>
    /// 它属于标签条整体，不属于某一个标签页 —— 由 <c>ForgeEditorControl</c> 在标签条底边画一条 1px。
    /// expUI Light 没定义这个键，回落 `Borders.color`（与条带边框同色）。
    /// </remarks>
    public required Color EditorTabsBorder { get; init; }

    /// <summary>
    /// 编辑区正文的底色。
    /// </summary>
    /// <remarks>
    /// **不是 <see cref="Background"/>**：那个是工具窗口的底色（expUI 里 Gray13 / Gray2），
    /// 编辑区比它更「白」（Light）/ 更「深」（Dark）—— 上游的编辑区底色来自配色方案，
    /// Islands 里则是显式的 <c>editor-bg</c>。
    /// <para>
    /// 两者混用的后果不只是差一档灰：选中的标签页恰好是编辑区底色
    /// （<see cref="EditorTabSelectedBackground"/>，expUI 的选中态只有下划线），
    /// 底色若取成工具窗口那档，选中标签就成了贴在灰底上的一块白补丁；
    /// 而分屏之间那条 1px 的线（<see cref="EditorTabsBorder"/>）也会因为
    /// 与底色只差一档而**看不见**。
    /// </para>
    /// </remarks>
    public required Color EditorBackground { get; init; }

    /// <summary>
    /// 按宿主当前的明暗变体挑一套 **expUI** 配色。
    /// </summary>
    /// <remarks>
    /// 宿主的明暗通常是跟随系统的；钉死一套会出现「软件其余部分变白、外壳还是黑的」那道硬边。
    /// 未知变体按浅色处理，与 Avalonia 的默认一致。
    /// <para>
    /// 这是 <see cref="ForTheme"/> 传 <see cref="ForgeThemeFamily.ExpUi"/> 的薄封装，
    /// 保留是为了让 Islands 落地前写的宿主一行不用改。
    /// </para>
    /// </remarks>
    /// <param name="variant">明暗变体。</param>
    /// <returns>该用的配色。</returns>
    public static ForgePalette ForVariant(ThemeVariant? variant)
        => ForTheme(ForgeThemeFamily.ExpUi, variant);

    /// <summary>
    /// 按配色族 × 明暗变体挑一套。
    /// </summary>
    /// <remarks>
    /// 两个维度正交：族决定形态与色阶（方角 / 岛），变体决定明暗。
    /// 未知变体按浅色处理。
    /// </remarks>
    /// <param name="family">配色族。</param>
    /// <param name="variant">明暗变体。</param>
    /// <returns>该用的配色。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="family"/> 不是已知的族。</exception>
    public static ForgePalette ForTheme(ForgeThemeFamily family, ThemeVariant? variant)
    {
        var dark = variant == ThemeVariant.Dark;

        return family switch
        {
            ForgeThemeFamily.ExpUi => dark ? ExpUiDark : ExpUiLight,
            ForgeThemeFamily.Islands => dark ? IslandsDark : IslandsLight,
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, null),
        };
    }

    /// <summary>expUI Light。</summary>
    public static ForgePalette ExpUiLight { get; } = new()
    {
        Background = ForgeColor.Parse("#F7F8FA"),                       // Gray13
        // expUI 没有「海」：面板彼此贴死，主窗口底色即面板底色。Island 为 null，缝根本不画。
        MainBackground = ForgeColor.Parse("#F7F8FA"),
        IslandBorder = ForgeColor.Parse("#F7F8FA"),                     // 非岛形态，不参与绘制
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
        EditorTabSelectedBackground = ForgeColor.Parse("#FFFFFF"),   // Gray14 —— 与编辑区底色相同，expUI 的选中态只有下划线
        EditorTabSelectedBorder = ForgeColor.Parse("#00000000"),       // expUI 未定义
        EditorTabHoverBackground = ForgeColor.Parse("#FFFFFF00"),      // 末两位 alpha=00：expUI 没有悬停底
        EditorBackground = ForgeColor.Parse("#FFFFFF"),   // Gray14 —— 编辑区底色（同 EditorTabSelectedBackground）
        EditorTabsBorder = ForgeColor.Parse("#EBECF0"),   // 未定义，回落 Borders.color
        EditorTabInactiveSelectedBackground = ForgeColor.Parse("#FFFFFF"),  // expUI 未定义，取编辑区底色 / 不描边
        EditorTabInactiveSelectedBorder = ForgeColor.Parse("#00000000"),
    };

    /// <summary>expUI Dark。</summary>
    public static ForgePalette ExpUiDark { get; } = new()
    {
        Background = ForgeColor.Parse("#2B2D30"),                       // *.background / Gray2
        MainBackground = ForgeColor.Parse("#2B2D30"),                   // 同 Light：expUI 无「海」
        IslandBorder = ForgeColor.Parse("#2B2D30"),                     // 非岛形态，不参与绘制
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
        EditorTabSelectedBackground = ForgeColor.Parse("#1E1F22"),   // Gray1 —— 同上
        EditorTabSelectedBorder = ForgeColor.Parse("#00000000"),       // expUI 未定义
        EditorTabHoverBackground = ForgeColor.Parse("#1E1F2200"),      // 同上
        EditorBackground = ForgeColor.Parse("#1E1F22"),   // Gray1 —— 编辑区底色（同 EditorTabSelectedBackground）
        EditorTabsBorder = ForgeColor.Parse("#393B40"),   // Gray3
        EditorTabInactiveSelectedBackground = ForgeColor.Parse("#1E1F22"),  // expUI 未定义，取编辑区底色 / 不描边
        EditorTabInactiveSelectedBorder = ForgeColor.Parse("#00000000"),
    };

    /// <summary>
    /// Islands Light（上游 <c>ManyIslandsLight.theme.json</c>，Rider 主题菜单里的「Islands Light」）。
    /// </summary>
    /// <remarks>
    /// **白岛浮在灰海上**：<c>tool-window-bg = layer-2-bg = white</c>、
    /// <c>main-window-bg = layer-0-bg = gray-150</c>。
    /// ⚠ 这两个别名的层号与 Dark **不一样**，照 Dark 的映射解 Light 会把黑白关系做反，
    /// 见 <c>docs/upstream/Islands主题.md</c> §3。
    /// </remarks>
    public static ForgePalette IslandsLight { get; } = new()
    {
        Family = ForgeThemeFamily.Islands,
        Island = ForgeIslandMetrics.Default,
        Background = ForgeColor.Parse("#FFFFFF"),                       // tool-window-bg ← layer-2-bg ← white
        MainBackground = ForgeColor.Parse("#E9EAEE"),                   // main-window-bg ← layer-0-bg ← gray-150
        IslandBorder = ForgeColor.Parse("#FFFFFF"),                     // Island.borderColor ← tool-window-bg
        StripeBackground = ForgeColor.Parse("#E9EAEE"),                 // Stripe.background ← main-window-bg
        // ToolWindow.Stripe.borderColor / ToolWindow.borderColor 在 Islands 下都是 transparent：
        // 岛与海之间已经有缝，再描一圈就是 Islands 要去掉的那条线。
        StripeBorder = ForgeColor.Parse("#00000000"),
        WindowBorder = ForgeColor.Parse("#00000000"),
        StripeSeparator = ForgeColor.Parse("#00000020"),                // toolbar-border ← transparent-black-30
        StripeSeparatorDragAndDrop = ForgeColor.Parse("#3871E1"),       // control-brand-border ← blue-80
        ButtonForeground = ForgeColor.Parse("#5F6269"),                 // text-muted ← gray-70
        ButtonSelectedForeground = ForgeColor.Parse("#FFFFFF"),         // icon-over-accent ← white
        ButtonSelectedBackground = ForgeColor.Parse("#3871E1"),         // toolbar-selected-bg-active ← blue-80
        ButtonPressedBackground = ForgeColor.Parse("#0000001D"),        // ActionButton.pressedBackground（未覆盖，沿用）
        ButtonHoverBackground = ForgeColor.Parse("#00000012"),          // ActionButton.hoverBackground（未覆盖，沿用）
        ButtonDropBackground = ForgeColor.Parse("#3671E580"),           // 带 alpha，主题里直接写死
        ButtonDropBorder = ForgeColor.Parse("#3871E1"),
        DragAreaBackground = ForgeColor.Parse("#366ACF4D"),             // 带 alpha
        HeaderBackground = ForgeColor.Parse("#FFFFFF"),                 // Header.inactiveBackground ← tool-window-bg
        HeaderBorder = ForgeColor.Parse("#E9EAEE"),                     // tool-window-border ← layer-2-border ← gray-150
        HeaderTabHoverBackground = ForgeColor.Parse("#00000012"),       // tab-bg-hovered ← transparent-black-20
        HeaderTabUnderline = ForgeColor.Parse("#3871E1"),
        HeaderTabInactiveUnderline = ForgeColor.Parse("#D1D3D9"),       // gray-130
        Foreground = ForgeColor.Parse("#000000"),                       // text-default ← black
        EditorTabSelectedBackground = ForgeColor.Parse("#E3EBFE"),   // tab-selected-bg-active ← blue-150
        EditorTabSelectedBorder = ForgeColor.Parse("#A7C5FF"),       // tab-selected-border-active ← blue-120
        EditorTabHoverBackground = ForgeColor.Parse("#00000012"),      // tab-bg-hovered ← transparent-black-20
        EditorBackground = ForgeColor.Parse("#FFFFFF"),   // editor-bg ← layer-2-bg ← white
        EditorTabsBorder = ForgeColor.Parse("#E9EAEE"),   // editor-border ← layer-2-border ← gray-150
        EditorTabInactiveSelectedBackground = ForgeColor.Parse("#E9EAEE"),  // tab-selected-bg-inactive / tab-selected-border-inactive
        EditorTabInactiveSelectedBorder = ForgeColor.Parse("#D1D3D9"),
    };

    /// <summary>
    /// Islands Dark（上游 <c>ManyIslandsDark.theme.json</c>，Rider 主题菜单里的「Islands Dark」）。
    /// </summary>
    /// <remarks>
    /// **暗岛浮在稍亮的海上**：<c>tool-window-bg = layer-0-bg = gray-10</c>、
    /// <c>main-window-bg = layer-1-bg = gray-30</c>。层号与 Light 相反，见该类 <see cref="IslandsLight"/> 的注释。
    /// </remarks>
    public static ForgePalette IslandsDark { get; } = new()
    {
        Family = ForgeThemeFamily.Islands,
        Island = ForgeIslandMetrics.Default,
        Background = ForgeColor.Parse("#191A1C"),                       // tool-window-bg ← layer-0-bg ← gray-10
        MainBackground = ForgeColor.Parse("#26282C"),                   // main-window-bg ← layer-1-bg ← gray-30
        IslandBorder = ForgeColor.Parse("#191A1C"),                     // Island.borderColor ← tool-window-bg
        StripeBackground = ForgeColor.Parse("#26282C"),                 // Stripe.background ← main-window-bg
        StripeBorder = ForgeColor.Parse("#00000000"),                   // transparent，理由同 Light
        WindowBorder = ForgeColor.Parse("#00000000"),                   // transparent
        StripeSeparator = ForgeColor.Parse("#FFFFFF21"),                // toolbar-border ← transparent-white-30
        StripeSeparatorDragAndDrop = ForgeColor.Parse("#3871E1"),       // control-brand-border ← blue-80
        ButtonForeground = ForgeColor.Parse("#9FA2A8"),                 // text-muted ← gray-100
        ButtonSelectedForeground = ForgeColor.Parse("#FFFFFF"),         // icon-over-accent ← white
        ButtonSelectedBackground = ForgeColor.Parse("#3871E1"),         // toolbar-selected-bg-active ← blue-80
        ButtonPressedBackground = ForgeColor.Parse("#FFFFFF26"),        // 未覆盖，沿用 expUI Dark
        ButtonHoverBackground = ForgeColor.Parse("#FFFFFF16"),          // 未覆盖，沿用 expUI Dark
        ButtonDropBackground = ForgeColor.Parse("#3671E580"),
        ButtonDropBorder = ForgeColor.Parse("#3871E1"),
        DragAreaBackground = ForgeColor.Parse("#366ACF4D"),
        HeaderBackground = ForgeColor.Parse("#191A1C"),                 // Header.inactiveBackground ← tool-window-bg
        HeaderBorder = ForgeColor.Parse("#26282C"),                     // tool-window-border ← layer-0-border ← gray-30
        HeaderTabHoverBackground = ForgeColor.Parse("#FFFFFF17"),       // tab-bg-hovered ← transparent-white-20
        HeaderTabUnderline = ForgeColor.Parse("#3871E1"),
        HeaderTabInactiveUnderline = ForgeColor.Parse("#5A5D63"),       // 未覆盖，沿用 expUI Dark
        Foreground = ForgeColor.Parse("#D1D3D9"),                       // text-default ← gray-130
        EditorTabSelectedBackground = ForgeColor.Parse("#233558"),   // tab-selected-bg-active ← blue-40
        EditorTabSelectedBorder = ForgeColor.Parse("#2E4D89"),       // tab-selected-border-active ← blue-60
        EditorTabHoverBackground = ForgeColor.Parse("#FFFFFF17"),      // tab-bg-hovered ← transparent-white-20
        EditorBackground = ForgeColor.Parse("#191A1C"),   // editor-bg ← layer-0-bg ← gray-10
        EditorTabsBorder = ForgeColor.Parse("#26282C"),   // editor-border ← layer-0-border ← gray-30
        EditorTabInactiveSelectedBackground = ForgeColor.Parse("#26282C"),  // tab-selected-bg-inactive / tab-selected-border-inactive
        EditorTabInactiveSelectedBorder = ForgeColor.Parse("#40434A"),
    };
}

/// <summary>
/// 配色族——决定形态与色阶，与明暗变体正交。
/// </summary>
/// <remarks>
/// 出处见 <c>docs/upstream/Islands主题.md</c>。
/// 「族 × 明暗」两维之所以不压成一个枚举，是因为宿主的明暗通常跟随系统、而族是用户选的，
/// 两者的来源和变更时机都不一样。
/// </remarks>
public enum ForgeThemeFamily
{
    /// <summary>expUI（Rider 主题菜单里的 <c>Dark</c> / <c>Light</c>）：方角，面板彼此贴死。</summary>
    ExpUi = 0,

    /// <summary>Islands（<c>Islands Dark</c> / <c>Islands Light</c>）：圆角，面板留缝浮在底色上。</summary>
    Islands = 1,
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
