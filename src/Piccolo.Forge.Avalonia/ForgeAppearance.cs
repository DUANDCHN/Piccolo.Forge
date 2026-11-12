using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 外壳的外观：一个对象，两个控件共用。
/// </summary>
/// <remarks>
/// <b>为什么是一个对象，而不是各控件一堆属性。</b>
/// 外框（<see cref="ForgeControl"/>）与编辑区（<see cref="ForgeEditorControl"/>）是两个独立控件，
/// 但「配色族 / 取色 / 跟随明暗 / 岛几何」说的是**同一件事**，另有两对是同一件事的两半
/// （面板标题条 ↔ 文档标签行的字重与行高）。早先这些是两边各一份的散装属性，代价有三笔：
/// <list type="number">
/// <item>宿主每样都要设两遍，漏一边就得到「四边是岛、中间还是方角」；</item>
/// <item>库内部每个传播点都得手抄一遍 —— <c>ForgeEditorControl.FloatOut</c> 就抄漏了三个
/// （取色回调、字重、行高），表现是文档浮出去之后描边没了、字重和行高变回默认；</item>
/// <item>每加一个外观属性要改四处（两个控件 + 宿主两处），加错一处不报错、只是看着不对。</item>
/// </list>
/// 打成一个不可变对象之后，传播就是传一个引用，**漏抄在语法上不可能**；
/// 加字段只改这一个 record。
/// <para>
/// <b>怎么改一处值。</b>它是 record，用 <c>with</c>：
/// <code>
/// frame.Appearance = frame.Appearance with { Family = ForgeThemeFamily.Islands };
/// </code>
/// 整个对象换掉才有变更通知 —— 这也是它做成不可变的原因：可变对象改字段没人知道，
/// 就得再发明一套通知，那正是散装属性的老问题换个地方复发。
/// </para>
/// <para>
/// <b>怎么送到控件上。</b>见 <see cref="ForgeChrome.AppearanceProperty"/> —— 它是**继承式**附加属性，
/// 设在外框上就自动流到树里的编辑区，宿主只设一处。
/// </para>
/// </remarks>
public sealed record ForgeAppearance
{
    /// <summary>什么都不改的那一份（库默认 = 上游取值）。</summary>
    /// <remarks>宿主从它 <c>with</c> 出自己那份，别自己 <c>new</c> —— 将来加字段时默认值只在这里定一次。</remarks>
    public static ForgeAppearance Default { get; } = new();

    /// <summary>
    /// 配色族——方角的 expUI 还是圆角留缝的 Islands。默认 <see cref="ForgeThemeFamily.ExpUi"/>。
    /// </summary>
    /// <remarks>
    /// 与明暗**正交**：换族不影响「跟随系统明暗」，两个维度各走各的。
    /// 这也是宿主该用的入口——直接赋控件的 <c>Palette</c> 会被当成「宿主自己管配色」，
    /// 从此不再跟随系统明暗（见 <see cref="FollowThemeVariant"/>）。
    /// </remarks>
    public ForgeThemeFamily Family { get; init; }

    /// <summary>
    /// 配色是否跟着宿主的明暗变体走。默认 <see langword="true"/>。
    /// </summary>
    /// <remarks>
    /// 宿主的明暗通常跟随系统。钉死一套的结果是系统切浅色时
    /// 「软件其余部分变白、外壳还是黑的」，中间一道硬边。
    /// 想自己控制配色就把它设成 <see langword="false"/> 再赋控件的 <c>Palette</c>。
    /// </remarks>
    public bool FollowThemeVariant { get; init; } = true;

    /// <summary>
    /// 跟随明暗时用哪套配色 —— 不给就用库内置的那套（照上游主题抄的）。
    /// </summary>
    /// <remarks>
    /// 内置那两套是**上游主题的移植**，不该为某个项目改掉。项目要微调
    /// （比如岛与自家窗口底板对比不够），在这里拿内置那套 <c>with</c> 出一套自己的：
    /// <code>PaletteResolver = (family, variant) =>
    ///     ForgePalette.ForTheme(family, variant) with { Background = ... };</code>
    /// </remarks>
    public Func<ForgeThemeFamily, ThemeVariant, ForgePalette>? PaletteResolver { get; init; }

    /// <summary>
    /// 岛的几何（圆角、缝宽、描边）。<see langword="null"/> 表示用配色自带的
    /// <see cref="ForgePalette.Island"/>，即上游取值。
    /// </summary>
    /// <remarks>
    /// 给宿主留的调节口——观感这件事各家软件口味不同，圆角大小尤其。
    /// 默认值仍是照上游抄的，覆盖是**宿主的选择**，不是库的默认。
    /// <para>
    /// 只在岛形态下有意义：配色的 <see cref="ForgePalette.Island"/> 为 <see langword="null"/>
    /// （expUI）时，这里设了也不会凭空变成岛——形态由族决定，本属性只调几何。
    /// </para>
    /// <para>
    /// ⚠ 外框与编辑区**必须同一份**，否则四边是岛、中间还是方角。它在这个对象里，
    /// 就是为了让这件事没法弄错。
    /// </para>
    /// <example>把圆角调小一半：<c>appearance with { IslandMetrics = ForgeIslandMetrics.Default with { Arc = 10 } }</c></example>
    /// </remarks>
    public ForgeIslandMetrics? IslandMetrics { get; init; }

    /// <summary>面板标题条上那行字的字重。</summary>
    /// <remarks>上游是常规字重，所以默认 <see cref="FontWeight.Normal"/>。</remarks>
    public FontWeight HeaderTitleFontWeight { get; init; } = FontWeight.Normal;

    /// <summary>
    /// 面板标题条上那行**面板名**的字号；<see cref="double.NaN"/> = 不覆盖，走控件默认（12）。
    /// </summary>
    /// <remarks>
    /// ⚠ 只管**面板名**。标题条上的内容标签（<c>ForgeContentTab</c>）与文档标签
    /// （<c>ForgeEditorTab</c>）各有各的字号，不吃这一支 —— 三者本来就允许不一样大。
    /// </remarks>
    public double HeaderTitleFontSize { get; init; } = double.NaN;

    /// <summary>
    /// 停靠着的面板标题条多高；<see cref="double.NaN"/> = 不指定，走上游那两档（紧凑 31 / 常规 41）。
    /// </summary>
    /// <remarks>
    /// 这是给宿主的**版面取舍**口子，不是紧凑档的开关：紧凑是一整档密度（条带按钮、字号、圆角一起变），
    /// 而"标题条该多高"常常只想单改这一行。想跟着密度走就别设它。
    /// <para>浮动形态不吃这个值 —— 那时标题条兼任窗口标题栏，按 <c>ForgeControl.TitleBarHeight</c> 长。</para>
    /// </remarks>
    public double HeaderHeight { get; init; } = double.NaN;

    /// <summary>
    /// 面板标题条上，标题距左缘多远；<see cref="double.NaN"/> = 走上游
    /// （<c>TabLayoutStart</c> 4 + <c>HeaderLabelInsetLeft</c> 12 = 16）。
    /// </summary>
    /// <remarks>
    /// ⚠ <b>它同时挪标题与标签的起点。</b>标签是从「标题右缘」往后排的
    /// （<c>TabContentLayout.java:178-184</c>），只挪标题不挪起点会在两者之间留一段空。
    /// 所以这个值一处给、两处用（见 <c>ForgeToolWindowHeader.ComputeTabsStart</c>）。
    /// <para>
    /// 与 <see cref="HeaderHeight"/> 同类：给宿主的**版面取舍**口子，不是密度开关。
    /// </para>
    /// </remarks>
    public double HeaderTitleInsetLeft { get; init; } = double.NaN;

    /// <summary>标题及标题后缀右侧的间距；默认采用上游 <c>ForgeMetrics.HeaderLabelInsetRight</c>。</summary>
    /// <remarks>宿主紧凑布局覆盖项，不改变图标尺寸；按钮宽度由 HeaderButtonWidth 独立设置。</remarks>
    public double HeaderTitleInsetRight { get; init; } = ForgeMetrics.HeaderLabelInsetRight;

    /// <summary>标题栏右侧工具栏的左内边距；默认采用上游 <c>ForgeMetrics.HeaderToolbarInsetLeft</c>。</summary>
    /// <remarks>宿主紧凑布局覆盖项，不改变图标尺寸；按钮宽度由 HeaderButtonWidth 独立设置。</remarks>
    public double HeaderToolbarInsetLeft { get; init; } = ForgeMetrics.HeaderToolbarInsetLeft;

    /// <summary>标题栏右侧工具栏的右内边距；默认采用上游 <c>ForgeMetrics.HeaderToolbarInsetRight</c>。</summary>
    /// <remarks>宿主紧凑布局覆盖项，不改变图标尺寸；按钮宽度由 HeaderButtonWidth 独立设置。</remarks>
    public double HeaderToolbarInsetRight { get; init; } = ForgeMetrics.HeaderToolbarInsetRight;

    /// <summary>面板标题栏图标按钮宽度，默认 24；可缩至图标槽宽度 20，高度与图标尺寸不变。</summary>
    /// <remarks>宿主布局覆盖项，所有面板包含内置按钮共用；分隔线仍由自身宽度和 Margin 决定。</remarks>
    public double HeaderButtonWidth { get; init; } = ForgeMetrics.HeaderButtonSize;

    /// <summary>
    /// 条带按钮上那块**高亮底**的边长；<see cref="double.NaN"/> = 按上游算（按钮 − 图标内边距）。
    /// </summary>
    /// <remarks>与 <see cref="StripeHighlightInsetHorizontal"/> **成对给**，见那里。图标不受它影响。</remarks>
    public double StripeHighlightSize { get; init; } = double.NaN;

    /// <summary>
    /// 那块高亮底的**左右边距**；<see cref="double.NaN"/> = 不指定（宽高走上游的按钮尺寸）。
    /// </summary>
    /// <remarks>
    /// ★ 这是"侧边栏该多宽"的正确问法：**别钉宽度，钉方底大小与两侧边距**，
    /// 宽度让它撑开 —— <c>条带宽 = 边距 + 方底 + 边距</c>。
    /// 这么定之后<b>居中是构造上成立的</b>，不会出现"宽度减尺寸是奇数、
    /// 居中落半像素、取整后偏一边"那种事。
    /// <para>
    /// 给了这一对之后，内侧那条 1px 线改成**压在内容上**、不再占宽
    /// （<c>ForgeToolWindowToolbar.BorderOverlaysContent</c>），否则总宽又会多出 1。
    /// </para>
    /// </remarks>
    public double StripeHighlightInsetHorizontal { get; init; } = double.NaN;

    /// <summary>
    /// 那块高亮底的**上下边距**；<see cref="double.NaN"/> = 与左右同值（方按钮）。
    /// </summary>
    /// <remarks>
    /// ★ <b>上下相邻两块底之间的空白 = 这个值 × 2</b>（按钮之间没有间隙，
    /// 上游是 <c>VerticalFlowLayout(0, 0)</c>）—— 想让两块底相距 6px 就给 3。
    /// <b>高度不用给</b>：<c>高 = 上边距 + 底 + 下边距</c>，与宽度同一条构造。
    /// </remarks>
    public double StripeHighlightInsetVertical { get; init; } = double.NaN;

    /// <summary>
    /// 条带上图标的边长；<see cref="double.NaN"/> = 走上游（常规 20 / 紧凑 16）。
    /// </summary>
    /// <remarks>
    /// **不影响按钮尺寸**：宽高由「边距 + 方底 + 边距」撑，图标只是在方底里居中 ——
    /// 所以调它只改图标本身的大小。
    /// </remarks>
    public double StripeIconSize { get; init; } = double.NaN;

    /// <summary>
    /// 条带按钮那块背板的圆角**半径**；<see langword="null"/> = 走上游
    /// （<see cref="ForgeMetrics.StripeButtonArc"/> = 12 是**直径**，即半径 6）。
    /// </summary>
    /// <remarks>
    /// ⚠ 这里收的是**半径**（Avalonia 的 <c>CornerRadius</c> 就是半径），
    /// 而上游那些 <c>arc</c> 是**直径**（IntelliJ 的 <c>RoundRectangle2D</c> 收 arcw/arch）。
    /// 库内部会折半，宿主这一层不必再换算 —— 免得"12 到底是 12 还是 6"每次都要想一遍。
    /// </remarks>
    public double? StripeButtonCornerRadius { get; init; }

    /// <summary>
    /// 「显示名字」时那块高亮底多宽；<see cref="double.NaN"/> = 走上游
    /// （<see cref="ForgeMetrics.StripeNamedDefaultWidth"/> = 59，那时它是**条带宽**）。
    /// </summary>
    /// <remarks>
    /// 与收起态同一条构造：<c>条带宽 = 边距 + 底 + 边距</c>
    /// （边距用 <see cref="StripeHighlightInsetHorizontal"/>），所以两侧留白天然相等。
    /// **高度不用给** —— 名字按这个宽度折行，折几行按钮就有多高。
    /// </remarks>
    public double StripeNamedHighlightWidth { get; init; } = double.NaN;

    /// <summary>
    /// 条带按钮上名字的字号；<see cref="double.NaN"/> = 走上游（图标边长的一半，不小于 8）。
    /// </summary>
    /// <remarks>
    /// ⚠ 它**不吃宿主的全局字号**：模板里那个 <c>TextBlock</c> 绑的是控件的属性（local value），
    /// 盖过 <c>Style Selector="TextBlock"</c> 那类设定 —— 想改只能从这里改。
    /// </remarks>
    public double StripeNameFontSize { get; init; } = double.NaN;

    /// <summary>
    /// 条带按钮上名字的字重；<see langword="null"/> = 跟着宿主的全局字重走。
    /// </summary>
    /// <remarks>小字压在云母那种半透明底上时，常规字重会发虚，压重一档更实。</remarks>
    public FontWeight? StripeNameFontWeight { get; init; }

    /// <summary>
    /// 显名时**内容与背板之间再让开多少**（上下各这么多）；<c>0</c> = 不让。
    /// </summary>
    /// <remarks>内容整块下挪这么多、按钮高加两份，于是背板跟着长出上下各这么多留白。</remarks>
    public double StripeNamedContentInset { get; init; }

    /// <summary>文档标签上那行字的字重。</summary>
    /// <remarks>上游是常规字重，所以默认 <see cref="FontWeight.Normal"/>。</remarks>
    public FontWeight TabTitleFontWeight { get; init; } = FontWeight.Normal;

    /// <summary>
    /// 文档标签行多高；<see cref="double.NaN"/> = 不指定，走上游按族算出的行高
    /// （<see cref="ForgeMetrics.EditorTabRowHeight"/>：Islands 40 / expUI 42）。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="HeaderHeight"/> 是同一件事的两半：中央文档区的标签行常常要与四周面板的标题条
    /// **一样高**，而这两个高度在上游本就不是一个数（40/42 对 41），对齐只能由宿主说了算。
    /// 两个字段挨着放，就是为了让"只改了一半"一眼看得出来。
    /// </remarks>
    public double TabRowHeight { get; init; } = double.NaN;
}

/// <summary>
/// 把 <see cref="ForgeAppearance"/> 挂到控件树上的附加属性。
/// </summary>
/// <remarks>
/// 单独一个静态类当宿主，是为了让外框与编辑区共用**同一个**属性 ——
/// 注册在其中一个控件上的话，另一个就得 <c>AddOwner</c> 它，等于让编辑区依赖外框类型，
/// 而编辑区是能独立使用的（浮出去的那些窗口里就没有外框）。
/// </remarks>
public sealed class ForgeChrome
{
    /// <remarks>
    /// 只当属性宿主用，不该有实例 —— 但也不能写成 <c>static class</c>：
    /// <c>RegisterAttached</c> 要拿它当类型参数，而静态类型不能做类型参数（CS0718）。
    /// </remarks>
    private ForgeChrome()
    {
    }

    /// <summary>
    /// 外观，**继承式**：设在外框上，树里的编辑区自动跟着，宿主只设一处。
    /// </summary>
    /// <remarks>
    /// 继承走的是控件树，所以覆盖也照常规矩来：在某个控件上显式设一份，它和它的子树就用那份
    /// —— 这就是「编辑区想跟外框不一样」的口子，不必再给库加开关。
    /// <para>
    /// ⚠ 继承到不了**另一棵树**：面板/文档浮出去之后在自己的窗口里，与主窗口的树没有父子关系。
    /// 那几处由库显式把这个对象传过去（<c>ForgeEditorControl.FloatOut</c>、外部窗口的装配），
    /// 传的是整个对象，所以不存在"抄漏了哪个字段"。
    /// </para>
    /// </remarks>
    public static readonly AttachedProperty<ForgeAppearance> AppearanceProperty =
        AvaloniaProperty.RegisterAttached<ForgeChrome, Control, ForgeAppearance>(
            "Appearance", ForgeAppearance.Default, inherits: true);

    /// <summary>读这个控件（或它从上面继承到）的外观。</summary>
    /// <param name="control">控件。</param>
    /// <returns>外观；没人设过就是 <see cref="ForgeAppearance.Default"/>。</returns>
    public static ForgeAppearance GetAppearance(Control control) => control.GetValue(AppearanceProperty);

    /// <summary>给这个控件（连同它的子树）设一份外观。</summary>
    /// <param name="control">控件。</param>
    /// <param name="value">外观。</param>
    public static void SetAppearance(Control control, ForgeAppearance value)
        => control.SetValue(AppearanceProperty, value);
}
