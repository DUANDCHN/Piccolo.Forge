using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Piccolo.Forge.Model;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 条带上的一个工具窗口按钮。
/// </summary>
/// <remarks>
/// 度量对应 <c>JBUI.CurrentTheme.Toolbar</c> 那一族（<c>度量与主题键.md</c> §2）：
/// 40×40、图标 20、图标四周 padding 5、圆角 12（紧凑 8）。
/// 背景优先级照抄 <c>SquareStripeButton.kt:208-219 · getBackgroundColor</c>。
/// </remarks>
public class ForgeStripeButton : ForgeSquareButtonBase
{
    /// <summary><see cref="IsToolWindowVisible"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsToolWindowVisibleProperty =
        AvaloniaProperty.Register<ForgeStripeButton, bool>(nameof(IsToolWindowVisible));

    /// <summary><see cref="IsToolWindowActive"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsToolWindowActiveProperty =
        AvaloniaProperty.Register<ForgeStripeButton, bool>(nameof(IsToolWindowActive));

    /// <summary><see cref="HasBadge"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> HasBadgeProperty =
        AvaloniaProperty.Register<ForgeStripeButton, bool>(nameof(HasBadge));

    /// <summary><see cref="BadgeMargin"/> 的属性定义。</summary>
    public static readonly StyledProperty<Thickness> BadgeMarginProperty =
        AvaloniaProperty.Register<ForgeStripeButton, Thickness>(nameof(BadgeMargin));

    /// <summary><see cref="Text"/> 的属性定义。</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ForgeStripeButton, string?>(nameof(Text));

    /// <summary><see cref="Icon"/> 的属性定义。</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<ForgeStripeButton, object?>(nameof(Icon));

    /// <summary><see cref="ShowName"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> ShowNameProperty =
        AvaloniaProperty.Register<ForgeStripeButton, bool>(nameof(ShowName));

    /// <summary><see cref="Title"/> 的属性定义。</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ForgeStripeButton, string?>(nameof(Title));

    /// <summary><see cref="StripeWidth"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> StripeWidthProperty =
        AvaloniaProperty.Register<ForgeStripeButton, double>(nameof(StripeWidth));

    static ForgeStripeButton()
    {
        AffectsRender<ForgeStripeButton>(
            IsToolWindowVisibleProperty, IsToolWindowActiveProperty, ShowNameProperty, TitleProperty, IconProperty);
        AffectsMeasure<ForgeStripeButton>(
            ShowNameProperty, StripeWidthProperty, TitleProperty,
            NameFontSizeOverrideProperty, NameFontWeightOverrideProperty, NamedContentInsetProperty);

        // 名字进气泡提示 —— **条带按钮默认只有一颗 20px 的图标，不看提示根本不知道它是什么**。
        // 对位上游 SquareStripeButton.updateHelpTooltip()：它装的 HelpTooltip 是
        // 「名字 + 快捷键」，我们没有快捷键，就只有名字。
        // 「显示名字」开着时也照给：名字画在图标下面，但按第一个空格拆成最多两行、
        // 还受条带宽度裁剪（见 SplitTitle），提示里那份才是完整的。
        //
        // ⚠ 样式走宿主的默认提示样式，**不另做一套**。上游那个 HelpTooltip 是自成一套配色的
        //   （深底亮字 + 快捷键弱化一档），2026-08-20 照着做过一版：给 ForgePalette 加了三支
        //   ToolTip 颜色、两族各一份键控 ControlTheme、按钮挂树时按键取主题。
        //   代码路径全部验通了（用例断言主题确实解析到，反证也红过），**但实机看上去没变**——
        //   说明卡在更下游的某处（多半是 ToolTip 弹出层不吃这份 ControlTheme）。
        //   查不动就整套撤了，别再照那条路重试一遍。要做的话先解决"模板到底生效没有"，
        //   而不是从调色板那头开始铺。
        // ⚠ 顺带一条查出来的事实，省得下次再考古：**默认气泡本来就是反相的**，且是 Semi 明写的 ——
        //   Semi.Avalonia 的 Themes/Light/Tooltip.axaml 给 ToolTipForeground = SemiColorWhite、
        //   Dark 给 SemiColorBlack，底色两档同取 SemiColorTertiaryActive(SemiGrey7)，
        //   而那支灰本身在两档里就是反的（浅档 #41464C / 深档 #C6CACD）。
        //   所以"浅色主题却是深气泡"不是缺陷，上游 IntelliJ 的 HelpTooltip 同样如此
        //   （expUI 浅色档底色 Gray2 = #27282E）。
        TitleProperty.Changed.AddClassHandler<ForgeStripeButton>((b, _) => b.SyncToolTip());
        // 无障碍：按钮只有图标，读屏器要靠 AutomationProperties.Name 才知道它是哪个面板
        TitleProperty.Changed.AddClassHandler<ForgeStripeButton>(
            (b, _) => AutomationProperties.SetName(b, b.Title ?? string.Empty));

        // 两个状态写进伪类，模板据此选底色（见 Themes/ForgeStripeButton.axaml）。
        // 用伪类而不是样式类：它们是控件自己的状态，不该让宿主用 Classes 去改。
        IsToolWindowVisibleProperty.Changed.AddClassHandler<ForgeStripeButton>(
            (b, e) => b.PseudoClasses.Set(":toolwindow-visible", e.GetNewValue<bool>()));
        IsToolWindowActiveProperty.Changed.AddClassHandler<ForgeStripeButton>(
            (b, e) => b.PseudoClasses.Set(":toolwindow-active", e.GetNewValue<bool>()));
        HasBadgeProperty.Changed.AddClassHandler<ForgeStripeButton>(
            (b, e) => b.PseudoClasses.Set(":badge", e.GetNewValue<bool>()));

        // 显名会换一整套内边距键，几何跟着变
        ShowNameProperty.Changed.AddClassHandler<ForgeStripeButton>((b, _) => b.UpdateGeometry());
    }

    /// <summary>对应的工具窗口当前是否可见。</summary>
    public bool IsToolWindowVisible
    {
        get => GetValue(IsToolWindowVisibleProperty);
        set => SetValue(IsToolWindowVisibleProperty, value);
    }

    /// <summary>图标右上角亮不亮角标（模板里的 PART_Badge，伪类 <c>:badge</c>）。</summary>
    public bool HasBadge
    {
        get => GetValue(HasBadgeProperty);
        set => SetValue(HasBadgeProperty, value);
    }

    /// <summary>
    /// 角标（未读圆点）的外边距，供模板绑定 —— 让圆点钉在**背板**的右上角。
    /// </summary>
    /// <remarks>
    /// ⚠ 不能直接绑 <c>BackgroundInsets</c>：方底那一档（宿主收窄按钮时）的背板是
    /// "显式边长 + 居中"、Insets 为零 —— 角标按零边距贴到**整颗按钮**的右上角，
    /// 就飘到背板外面去了（实机看出来的）。所以按实际排布尺寸把背板矩形再算一遍，
    /// 圆点各向外挑 1px（设计注释里"挑出背板角 1px"的那 1px）。
    /// 在 <see cref="ArrangeOverride"/> 里算：与方底居中同一个理由 —— 度量用的
    /// ButtonWidth/Height 与实际排到的尺寸可能不等，Margin 相对的是后者。
    /// </remarks>
    public Thickness BadgeMargin
    {
        get => GetValue(BadgeMarginProperty);
        private set => SetValue(BadgeMarginProperty, value);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);

        // 背板矩形按实际尺寸重算（HighlightRect 内部已区分方底/铺满/显名三档）。
        var plate = HighlightRect(size);
        const double overhang = 1;
        BadgeMargin = new Thickness(
            0,
            Math.Max(0, plate.Top - overhang),
            Math.Max(0, size.Width - plate.Right - overhang),
            0);

        return size;
    }

    /// <summary>对应的工具窗口当前是否持有焦点。</summary>
    public bool IsToolWindowActive
    {
        get => GetValue(IsToolWindowActiveProperty);
        set => SetValue(IsToolWindowActiveProperty, value);
    }

    /// <summary>
    /// 条带按钮上的图标。
    /// </summary>
    /// <remarks>
    /// 是 <see cref="object"/>：宿主塞控件、几何图形或字符都行。
    /// Forge **不带图标资源**（上游的图标是 JetBrains 的美术资产，
    /// 原因见 <c>工具窗口模型.md</c> §17）；这里只按审出来的尺寸留位置：
    /// 图标区 <see cref="ForgeSquareButtonBase.IconSize"/>、四周 <see cref="ForgeSquareButtonBase.IconPadding"/>。
    /// </remarks>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>没有图标时画的占位字符。</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>是否在图标下面显示工具窗口的名字。</summary>
    /// <remarks>
    /// 开关是 `UISettings.showToolWindowsNames`，默认 false
    /// （<c>UISettingsState.kt:83</c>）；打开时条带宽度变成
    /// <see cref="ForgeMetrics.StripeNamedDefaultWidth"/>（<c>ResizeStripeManager.kt:215-218</c>）。
    /// </remarks>
    public bool ShowName
    {
        get => GetValue(ShowNameProperty);
        set => SetValue(ShowNameProperty, value);
    }

    /// <summary>
    /// 工具窗口的名字：<see cref="ShowName"/> 打开时画在图标下面，**任何时候都进气泡提示**。
    /// </summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>把名字同步进气泡提示。空名字就把提示摘掉，别留一个空气泡。</summary>
    private void SyncToolTip()
        => ToolTip.SetTip(this, string.IsNullOrWhiteSpace(Title) ? null : Title);

    /// <summary>条带的自定义宽度；<c>0</c> 表示按按钮自身尺寸走。</summary>
    public double StripeWidth
    {
        get => GetValue(StripeWidthProperty);
        set => SetValue(StripeWidthProperty, value);
    }

    /// <summary>名字左右的 padding：紧凑 4，否则 6。</summary>
    public double TextPadding
        => IsCompact ? ForgeCompactMetrics.StripeButtonTextPadding : ForgeMetrics.StripeButtonTextPadding;

    /// <summary>
    /// 当前该用哪个背景。
    /// </summary>
    /// <remarks>
    /// ⚠ <b>它已经不决定画面了</b>（REQ-067）：底色由模板里那层 <c>Border</c> 按伪类选，
    /// 见 <c>Themes/ForgeStripeButton.axaml</c>。这里留着是给**用例**当判据用的
    /// ——"这个按钮此刻该亮成哪一档"是个语义问题，从伪类反推不如直接问它。
    /// 两边的优先级必须一致，改一处要改两处；顺序照抄
    /// <c>SquareStripeButton.kt:208-219</c>：**焦点 &gt; 窗口可见 &gt; 悬停 &gt; 无**。
    /// </remarks>
    /// <returns>背景色；都不满足时是 <see langword="null"/>（不画）。</returns>
    public override Color? ResolveBackground()
    {
        if (IsToolWindowActive)
        {
            return Palette.ButtonSelectedBackground;
        }

        if (IsToolWindowVisible)
        {
            return Palette.ButtonPressedBackground;
        }

        return base.ResolveBackground();
    }

    /// <summary><see cref="SelectedBackgroundBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> SelectedBackgroundBrushProperty =
        AvaloniaProperty.Register<ForgeStripeButton, IBrush?>(nameof(SelectedBackgroundBrush));

    /// <summary><see cref="PressedBackgroundBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> PressedBackgroundBrushProperty =
        AvaloniaProperty.Register<ForgeStripeButton, IBrush?>(nameof(PressedBackgroundBrush));

    /// <summary>面板处于激活态时的底，由 <c>ControlTheme</c> 从资源键给。</summary>
    public IBrush? SelectedBackgroundBrush
    {
        get => GetValue(SelectedBackgroundBrushProperty);
        set => SetValue(SelectedBackgroundBrushProperty, value);
    }

    /// <summary>面板开着（但未激活）时的底，来源同上。</summary>
    public IBrush? PressedBackgroundBrush
    {
        get => GetValue(PressedBackgroundBrushProperty);
        set => SetValue(PressedBackgroundBrushProperty, value);
    }

    /// <inheritdoc />
    protected override IBrush? ResolveBackgroundBrush()
    {
        if (IsToolWindowActive)
        {
            return SelectedBackgroundBrush ?? Fallback(Palette.ButtonSelectedBackground);
        }

        if (IsToolWindowVisible)
        {
            return PressedBackgroundBrush ?? Fallback(Palette.ButtonPressedBackground);
        }

        return base.ResolveBackgroundBrush();
    }

    /// <summary>当前该用哪个前景。</summary>
    /// <remarks>激活时是 <c>ToolWindow.Button.selectedForeground</c>，否则 <c>ToolWindow.Button.foreground</c>。</remarks>
    /// <returns>前景色。</returns>
    public override Color ResolveForeground()
        => IsToolWindowActive ? Palette.ButtonSelectedForeground : Palette.ButtonForeground;

    /// <summary>
    /// 名字按第一个空格拆成最多两行。
    /// </summary>
    /// <remarks>对应 <c>SquareStripeButton.kt:331-334 · getStripeSplitText</c>：<c>split(" ", limit = 2)</c>。</remarks>
    /// <param name="title">工具窗口的名字。</param>
    /// <returns>拆出来的行。</returns>
    public static IReadOnlyList<string> SplitTitle(string? title)
    {
        var text = title?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var space = text.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? [text] : [text[..space], text[(space + 1)..]];
    }

    /// <summary><see cref="NameFontSize"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> NameFontSizeProperty =
        AvaloniaProperty.Register<ForgeStripeButton, double>(nameof(NameFontSize));

    /// <summary>「显示名字」时字号：比正文小一号。</summary>
    /// <remarks>
    /// 上游是 <c>RelativeFont.TINY.derive(button.font)</c>（<c>SquareStripeButton.kt:329</c>）。
    /// 做成 Avalonia 属性是因为模板要 <c>TemplateBinding</c> 它（普通 C# 属性绑不了）；
    /// 值由 <see cref="SyncName"/> 跟着密度一起刷。
    /// </remarks>
    public double NameFontSize
    {
        get => GetValue(NameFontSizeProperty);
        private set => SetValue(NameFontSizeProperty, value);
    }

    /// <summary><see cref="NamedContentInset"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> NamedContentInsetProperty =
        AvaloniaProperty.Register<ForgeStripeButton, double>(nameof(NamedContentInset));

    /// <summary>
    /// 显名时**内容与背板之间再让开多少**（上下各这么多），<c>0</c> = 不让。
    /// </summary>
    /// <remarks>
    /// 内容（图标 + 名字）整块往下挪这么多，按钮的高度加两份 —— 于是背板跟着长，
    /// 上下各多出这么多留白。图标那一格与名字的相对关系不变。
    /// <para>不影响收起态：那一档的背板是定死的方块，内容就是图标本身。</para>
    /// </remarks>
    public double NamedContentInset
    {
        get => GetValue(NamedContentInsetProperty);
        set => SetValue(NamedContentInsetProperty, value);
    }

    /// <summary><see cref="NameFontSizeOverride"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> NameFontSizeOverrideProperty =
        AvaloniaProperty.Register<ForgeStripeButton, double>(nameof(NameFontSizeOverride), double.NaN);

    /// <summary>
    /// 宿主指定的名字字号；<see cref="double.NaN"/> = 走上游（图标边长的一半，不小于 8）。
    /// </summary>
    /// <remarks>
    /// ⚠ 名字的字号**不吃宿主的全局字号**：模板里那个 <c>TextBlock</c> 绑的是
    /// <see cref="NameFontSize"/>，是 local value，盖过宿主 <c>Style Selector="TextBlock"</c>
    /// 那类样式设定 —— 所以想改只能从这里改。
    /// （字**重**相反：模板没设，所以它吃宿主的全局字重。）
    /// </remarks>
    public double NameFontSizeOverride
    {
        get => GetValue(NameFontSizeOverrideProperty);
        set => SetValue(NameFontSizeOverrideProperty, value);
    }

    /// <summary><see cref="NameFontWeightOverride"/> 的属性定义。</summary>
    public static readonly StyledProperty<FontWeight?> NameFontWeightOverrideProperty =
        AvaloniaProperty.Register<ForgeStripeButton, FontWeight?>(nameof(NameFontWeightOverride));

    /// <summary><see cref="NameFontWeight"/> 的属性定义。</summary>
    public static readonly StyledProperty<FontWeight> NameFontWeightProperty =
        AvaloniaProperty.Register<ForgeStripeButton, FontWeight>(nameof(NameFontWeight), FontWeight.Normal);

    /// <summary>
    /// 宿主指定的名字字重；<see langword="null"/> = **跟着继承来的字重走**（宿主的全局样式）。
    /// </summary>
    /// <remarks>
    /// 默认之所以是"跟着继承"而不是某个具体档：字重是宿主整套排版的一部分
    /// （本软件全局是 Medium），库不该替它定。想单独压重（小字在云母上发虚时）才给这个值。
    /// </remarks>
    public FontWeight? NameFontWeightOverride
    {
        get => GetValue(NameFontWeightOverrideProperty);
        set => SetValue(NameFontWeightOverrideProperty, value);
    }

    /// <summary>名字实际用的字重 —— 模板绑它。</summary>
    /// <remarks>
    /// ⚠ 模板一旦绑了它，这就是 local value，**继承链就断了** ——
    /// 所以它必须自己把"没覆盖时跟着继承走"实现出来：值 = 覆盖 ?? 控件自己的 <c>FontWeight</c>。
    /// </remarks>
    public FontWeight NameFontWeight
    {
        get => GetValue(NameFontWeightProperty);
        private set => SetValue(NameFontWeightProperty, value);
    }

    /// <summary>按当前密度算出的字号（<see cref="NameFontSize"/> 的来源）。</summary>
    /// <remarks>上游是 <c>RelativeFont.TINY</c>（<c>SquareStripeButton.kt:329</c>），这里取图标的一半。</remarks>
    private double ComputeNameFontSize()
        => double.IsNaN(NameFontSizeOverride) ? Math.Max(8, IconSize * 0.5) : NameFontSizeOverride;

    /// <summary>
    /// 名字的行高：上游的 <b>字号 × 1.4</b>，但**不小于这个字体在这个字号下的自然行高**。
    /// </summary>
    /// <remarks>
    /// ★ 后半句是实机 bug 换来的（REQ-067）：模板化之后名字是模板里的 <c>TextBlock</c>，
    /// 而 <c>TextBlock.LineHeight</c> 一旦**小于自然行高，Avalonia 会把字剪掉**
    /// —— 症状是"字只剩上半截"，且**只在某些字体 / 缩放下出现**：
    /// 自绘时代的 <c>FormattedText</c> 是"每行往下推 1.4 字高"，推得再密也只是行距挤，
    /// 字本身照画不误；换成 <c>TextBlock</c> 之后 1.4 em 成了**行盒的高度**，装不下就剪。
    /// 用例里那套默认字体 1.4 em 恰好够用，所以用例全绿也发现不了 ——
    /// 上位机用的是内嵌的 HarmonyOS Sans SC，它不够。
    /// <para>
    /// 取 <c>max</c> 而不是直接用自然行高：字体够用时逐像素保持上游的 1.4 em 行距，
    /// 只在真的装不下时才让开。<b>测量与模板必须用同一个值</b>，否则按钮高度与字对不上。
    /// </para>
    /// </remarks>
    /// <returns>行高。</returns>
    private double ComputeNameLineHeight()
    {
        var fontSize = ComputeNameFontSize();
        var pitch = fontSize * 1.4;
        var lines = SplitTitle(Title);
        if (lines.Count == 0)
        {
            return pitch;
        }

        // 用**真正要画的那行字**去量：中西文回退到的字体不同，自然行高也不同
        var natural = new FormattedText(
            lines[0],
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            ResolveTypeface(),
            fontSize,
            Brushes.Transparent).Height;

        return Math.Max(pitch, natural);
    }

    /// <summary><see cref="NameText"/> 的属性定义。</summary>
    public static readonly StyledProperty<string?> NameTextProperty =
        AvaloniaProperty.Register<ForgeStripeButton, string?>(nameof(NameText));

    /// <summary><see cref="NameMargin"/> 的属性定义。</summary>
    public static readonly StyledProperty<Thickness> NameMarginProperty =
        AvaloniaProperty.Register<ForgeStripeButton, Thickness>(nameof(NameMargin));

    /// <summary><see cref="NameLineHeight"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> NameLineHeightProperty =
        AvaloniaProperty.Register<ForgeStripeButton, double>(nameof(NameLineHeight), double.NaN);

    /// <summary>
    /// 模板里那个 <c>TextBlock</c> 要显示的字 —— 已经按上游规则折成最多两行。
    /// </summary>
    /// <remarks>
    /// 折行规则是<b>按第一个空格拆成两段</b>（<see cref="SplitTitle"/>，
    /// 对应 <c>SquareStripeButton.kt:331-334 · getStripeSplitText</c>），
    /// 不是按宽度自动换行 —— 所以这里用 <c>\n</c> 硬拼，模板那边不能开 <c>TextWrapping</c>，
    /// 否则长单词会被再切一刀、行数与量出来的高度对不上。
    /// </remarks>
    public string? NameText
    {
        get => GetValue(NameTextProperty);
        private set => SetValue(NameTextProperty, value);
    }

    /// <summary>名字那块相对按钮的位置。</summary>
    /// <remarks>
    /// ⚠ 名字贴着**图标的下沿**起，不是贴着按钮那一格的下沿
    /// （<c>SquareStripeButton.kt:247-257</c>：<c>iconPosition.y += iconHeight; y = 那个 + iconLabelGap</c>）。
    /// 图标在 40 的格子里居中（10..30），所以文字从 33 起；按钮总高 40+3+行高，
    /// 多出来的一截留在**下面**。
    /// 本仓原来从 <c>ButtonHeight(40)+3=43</c> 起画，图标与名字之间凭空多 10px，
    /// 看着就是"图标和文字离得太远"，而按钮底下反倒一点余地都没有。
    /// </remarks>
    public Thickness NameMargin
    {
        get => GetValue(NameMarginProperty);
        private set => SetValue(NameMarginProperty, value);
    }

    /// <summary>名字的行高（字号 × 1.4，与原先自绘时逐行推进的步长一致）。</summary>
    public double NameLineHeight
    {
        get => GetValue(NameLineHeightProperty);
        private set => SetValue(NameLineHeightProperty, value);
    }

    /// <summary>
    /// 名字那一截有多高 —— <b>问模板里那个 <c>TextBlock</c> 本人</b>。
    /// </summary>
    /// <remarks>
    /// ★ 实机 bug 两次都出在这（REQ-067）：名字是模板里的 <c>TextBlock</c>，
    /// 而它的高度由**字体的自然行高**决定；库这边照上游算的「字号 × 1.4」
    /// 只是个估计值，字体一换就不准 —— 估小了按钮装不下，字被剪掉半截。
    /// 上位机内嵌的 HarmonyOS Sans SC 就是这种。
    /// <para>
    /// 所以不再估：<c>ApplyTemplate()</c> 之后直接量那个 <c>TextBlock</c>，
    /// 它用的就是渲染时那套字体栈（含中西文回退），量出来多高就给多高，**结构上剪不着**。
    /// 模板还没套上（控件没进可视树）时才退回估计值。
    /// </para>
    /// </remarks>
    /// <param name="lines">名字折成了几行（模板还没套上时的兜底用）。</param>
    /// <param name="width">按钮的宽度 —— 名字按它折行。</param>
    /// <returns>名字那一截的高度。</returns>
    private double MeasureNameHeight(int lines, double width)
    {
        // 测量期间模板可能还没套上；这一句是幂等的
        ApplyTemplate();

        if (_nameBlock is { } block)
        {
            // ⚠ **先作废再量**：Layoutable.Measure 按 availableSize 缓存 ——
            //   "名字从空变成有字"（窄条带切到显名）这一步，若上一次是用同一个宽度量的，
            //   它会**直接返回旧的 DesiredSize**（空文本那 2px），于是名字排出来只有 2px 高、
            //   一个字都看不见。实机症状：启动时是窄条带，切到显名就没有字；
            //   而启动就是宽条带的反倒正常（那次量的就是有字的）。
            block.InvalidateMeasure();

            // ⚠ **必须按按钮的宽度量**：名字开了按宽度折行，用无限宽会量成一行、
            //   高度少算，那是另一个症状（字被剪掉下半截）。
            block.Measure(new Size(width, double.PositiveInfinity));
            return Math.Max(0, block.DesiredSize.Height - block.Margin.Top - block.Margin.Bottom);
        }

        return lines * ComputeNameLineHeight();
    }

    private TextBlock? _nameBlock;

    /// <inheritdoc />
    /// <remarks>
    /// ⚠ <b>套上模板之后必须重新测量。</b>名字那一截的高度是**问模板里那个 TextBlock 要**的
    /// （见 <see cref="MeasureNameHeight"/>），而控件还没进可视树时拿不到 <c>ControlTheme</c>
    /// —— 那一轮测量只能退回估计值。模板随后套上了，却没人再叫一次测量，
    /// 按钮就一直按估计值那么高：**估小了字就被剪掉半截**，正是实机看到的那样。
    /// </remarks>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _nameBlock = e.NameScope.Find<TextBlock>("PART_Name");
        InvalidateMeasure();
    }

    /// <summary>把名字的文本、位置、行高刷进那三个供模板绑定的属性。</summary>
    private void SyncName()
    {
        var lines = SplitTitle(Title);
        NameText = ShowName && lines.Count > 0 ? string.Join('\n', lines) : null;
        NameFontSize = ComputeNameFontSize();
        NameFontWeight = NameFontWeightOverride ?? FontWeight;
        NameLineHeight = ComputeNameLineHeight();

        // 竖直起点与自绘时同一条算式；左右留 TextPadding，文字在其中居中
        var top = IconRect(new Size(ButtonWidth, ButtonHeight)).Bottom + ForgeMetrics.StripeIconLabelGap;
        NameMargin = new Thickness(TextPadding, top, TextPadding, 0);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 显名与否会换一整套内边距键（<c>…StripeIconWithName.padding</c>），
    /// 所以基类要能问到它。
    /// </remarks>
    protected override bool ShowsName => ShowName;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (!ShowName)
        {
            return new Size(ButtonWidth, ButtonHeight);
        }

        // 高度 = 基础高 + 间距 + 名字那一截（SquareStripeButton.kt:320-327）；
        // **名字折几行就有多高** —— 长名字折成三行，按钮就跟着高一截。
        var lines = SplitTitle(Title).Count;
        var width = StripeWidth > 0 ? StripeWidth : ButtonWidth;
        var height = ButtonHeight
                     + (lines > 0
                         ? (NamedContentInset * 2) + ForgeMetrics.StripeIconLabelGap + MeasureNameHeight(lines, width)
                         : 0);

        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconProperty)
        {
            SetIconControl(change.GetNewValue<object?>() as Control);
        }

        // 图标 / 前景 / 激活态 / 族任一变化都要重喂那个画 Geometry 的子元素
        if (change.Property == IconProperty
            || change.Property == ForegroundProperty
            || change.Property == IsToolWindowActiveProperty
            || change.Property == PaletteProperty)
        {
            SyncGeometryIcon();
        }

        // 两个"面板开着 / 激活"的状态一变，底色跟着换
        if (change.Property == IsToolWindowVisibleProperty
            || change.Property == IsToolWindowActiveProperty
            || change.Property == SelectedBackgroundBrushProperty
            || change.Property == PressedBackgroundBrushProperty)
        {
            UpdateBackgroundBrush();
        }

        // 名字的文本 / 位置 / 行高跟着这些走
        if (change.Property == TitleProperty
            || change.Property == ShowNameProperty
            || change.Property == IsCompactProperty
            || change.Property == IsLeftStripeProperty
            || change.Property == NameFontSizeOverrideProperty
            || change.Property == NameFontWeightOverrideProperty
            || change.Property == NamedContentInsetProperty
            || change.Property == FontWeightProperty
            || change.Property == PaletteProperty)
        {
            SyncName();
        }
    }

    /// <summary>把该画的 Geometry 图标喂给模板里那个槽。</summary>
    /// <remarks>
    /// 三种情况：宿主给了 Geometry → 画它；什么都没给 → 画默认图形
    /// （<c>ForgeIcons.DefaultToolWindow</c>，那个空框）；给了控件或纯文字 → 不画。
    /// </remarks>
    private void SyncGeometryIcon()
    {
        var geometry = Icon as Geometry;
        if (geometry is null && Icon is null && string.IsNullOrEmpty(Text))
        {
            geometry = ForgeIcons.DefaultToolWindow;
        }

        SetGeometryIcon(geometry, ForegroundBrush, filled: false);
    }

    /// <summary>
    /// 自绘部分（Geometry 图标、名字）该用的画刷。
    /// </summary>
    /// <remarks>
    /// ★ 前景**由模板给**（REQ-067）：<c>ControlTheme</c> 按 <c>:toolwindow-active</c>
    /// 从资源键选色，写在 <see cref="TemplatedControl.Foreground"/> 上。这里只是拿来用。
    /// <para>
    /// 宿主塞进来的图标控件（Material.Icons 那类）默认**继承** <c>Foreground</c>，
    /// 所以同一条设置把三者一起管住了：自绘图标、名字文字、宿主的图标控件。
    /// </para>
    /// <para>
    /// 兜底成 <see cref="ResolveForeground"/>：控件还没进可视树时拿不到 <c>ControlTheme</c>
    /// （见 <c>ThemeResourceSpikeTests</c>），那时 <c>Foreground</c> 是 null。
    /// 兜底值与主题里的默认值同源（都出自 <see cref="ForgePalette"/>），所以看不出差别。
    /// </para>
    /// </remarks>
    private IBrush ForegroundBrush => Foreground ?? new SolidColorBrush(ResolveForeground());

    /// <summary>
    /// 图标占的那块地：<see cref="ForgeSquareButtonBase.IconSize"/> 见方，居中。
    /// </summary>
    /// <remarks>
    /// <b>图标尺寸是定值，不是「按钮减 padding」</b>：上游
    /// <c>SquareStripeButton.kt:356-367 · scaleIcon</c> 把图标直接缩放成
    /// <c>getStripeToolbarButtonIconSize()</c>（20，紧凑 16），再由 <c>ActionButton</c>
    /// 按 insets(5) 排进 40×40 的按钮里。
    /// <para>
    /// 第一版按 <c>Bounds - IconPadding × 2</c> 算，得 30×30 —— 图标被放大到 1.5 倍，
    /// 整条条带看着都糊。padding 决定的是<b>按钮内容区</b>，不是图标大小。
    /// </para>
    /// <para>
    /// 显示名字时图标只占上面那一块（<c>:240</c> 的 <c>buttonWrapper</c> 就是干这个）。
    /// </para>
    /// <para>
    /// <b>居中的基准是<see cref="ForgeSquareButtonBase.BackgroundRect"/>，不是整个按钮</b>——
    /// 上游 <c>SquareStripeButtonLook.kt:112-118 · getIconPosition</c> 与 <c>paintBackground</c>
    /// 扣的是**同一份** <c>iconPadding</c>，图标与高亮共用一个矩形。
    /// expUI 的内边距左右对称，两种算法恰好重合，所以按整块按钮居中一直看不出错；
    /// Islands 是 5/2 不对称，图标就会比高亮偏 1.5px。
    /// </para>
    /// </remarks>
    /// <param name="size">按钮自己的尺寸。</param>
    /// <returns>图标矩形。</returns>
    /// <inheritdoc />
    /// <remarks>显名时按钮被名字撑高，图标只在上面那一格里排。</remarks>
    protected override Rect ContentCell(Size size)
        => ShowName
            ? new Rect((size.Width - ButtonWidth) / 2, NamedContentInset, ButtonWidth, ButtonHeight)
            : new Rect(size);

    /// <inheritdoc />
    /// <remarks>
    /// ⚠ 基准是 <see cref="ContentCell"/>（图标那一格），**不是**
    /// <see cref="ForgeSquareButtonBase.HighlightRect"/>（整颗按钮的背板）。
    /// 显名时按钮被名字撑高，拿背板当基准，图标就在"图标+名字"里居中 ——
    /// 往下掉半截、压在字上（实机看出来的）。
    /// </remarks>
    protected override Rect IconRect(Size size)
    {
        var area = BackgroundRect(ContentCell(size));
        return new Rect(
            area.X + ((area.Width - IconSize) / 2),
            area.Y + ((area.Height - IconSize) / 2),
            IconSize,
            IconSize);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        // 整块都能点，不然只有画了东西的地方收得到鼠标
        ForgeHitArea.Fill(context, Bounds.Size);

        // 底色归模板里那层 Border（绑 BackgroundBrush）。矩形仍是「按钮矩形 − 图标内边距」，
        // 由 BackgroundInsets 表达（SquareStripeButtonLook.kt:64-70）；
        // ⚠ 显名时也是整块按钮、含名字那一截（SquareStripeButton.kt:196-198），别跟着图标一起砍。
        var iconRect = IconRect(Bounds.Size);

        // 图标一律由模板里那个槽画（宿主的控件图标，或画 Geometry 的子元素），
        // 位置都走 IconRect —— 见 ForgeSquareButtonBase.SetGeometryIcon。
        if (IconControl is not null || Icon is Geometry)
        {
            return;
        }

        if (string.IsNullOrEmpty(Text))
        {
            // 什么都没给时的默认图形也走那个槽（见 SyncGeometryIcon）
            return;
        }

        var text = new FormattedText(
            Text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            ResolveTypeface(),
            IconSize * 0.7,
            ForegroundBrush);

        context.DrawText(
            text,
            new Point(
                iconRect.X + ((iconRect.Width - text.Width) / 2),
                iconRect.Y + ((iconRect.Height - text.Height) / 2)));
    }


    /// <summary>
    /// 画文字用的字形 —— **走控件自己继承下来的字体**，不是 <see cref="Typeface.Default"/>。
    /// </summary>
    /// <remarks>
    /// 这些文字是自绘的，不像 <c>TextBlock</c> 那样自动吃到宿主的全局字体样式；
    /// 用 <see cref="Typeface.Default"/> 的话，整个外壳（面板标题、条带名字、标签页）
    /// 会与宿主其余部分**用两种字体**，宿主内嵌了字体时尤其明显。
    /// <see cref="TemplatedControl.FontFamily"/> 是可继承属性，读它就跟着宿主走。
    /// </remarks>
    private Typeface ResolveTypeface(FontWeight weight = FontWeight.Normal)
        => new(FontFamily, FontStyle.Normal, weight);
}

/// <summary>
/// 条带上分组之间的那条分隔符。
/// </summary>
/// <remarks>
/// 对应 <c>platform/platform-impl/src/com/intellij/openapi/wm/impl/StripeButtonSeparator.kt</c>：
/// 占位 32×11，中间画一条 24×1 的线，横向居中于**去掉 padding 后**的区域、纵向在正中。
/// </remarks>
public class ForgeStripeSeparator : TemplatedControl
{
    /// <summary><see cref="Palette"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgePalette> PaletteProperty =
        AvaloniaProperty.Register<ForgeStripeSeparator, ForgePalette>(nameof(Palette), ForgePalette.ExpUiDark);

    /// <summary><see cref="IsDragAndDrop"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsDragAndDropProperty =
        AvaloniaProperty.Register<ForgeStripeSeparator, bool>(nameof(IsDragAndDrop));

    /// <summary><see cref="LineMargin"/> 的属性定义。</summary>
    public static readonly StyledProperty<Thickness> LineMarginProperty =
        AvaloniaProperty.Register<ForgeStripeSeparator, Thickness>(nameof(LineMargin));

    static ForgeStripeSeparator()
    {
        AffectsRender<ForgeStripeSeparator>(PaletteProperty, IsDragAndDropProperty);

        // 拖拽态写进伪类，模板据此换色（ToolWindow.Stripe.separatorDragAndDropColor）
        IsDragAndDropProperty.Changed.AddClassHandler<ForgeStripeSeparator>(
            (x, e) => x.PseudoClasses.Set(":drag-and-drop", e.GetNewValue<bool>()));
    }

    /// <summary>
    /// 那条线在这块占位里的位置 —— 模板里的 <c>Border</c> 拿它当 <c>Margin</c>。
    /// </summary>
    /// <remarks>
    /// 值照抄原先 <c>Render</c> 里那两句：
    /// <code>
    /// x = 图标 padding(5) + (可画宽 - 线宽 24) / 2
    /// y = 高 / 2                       ← 是**从中线往下画**，不是垂直居中
    /// </code>
    /// ⚠ 那个 <c>y</c> 看着像"居中"，其实差半像素（11 高里线在 5.5，居中该是 5）。
    /// 照抄不"修正"——这是上游 <c>StripeButtonSeparator.kt:31-32</c> 的画法。
    /// </remarks>
    public Thickness LineMargin
    {
        get => GetValue(LineMarginProperty);
        private set => SetValue(LineMarginProperty, value);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var padding = ForgeMetrics.StripeButtonIconPadding;
        var paintAreaWidth = finalSize.Width - (padding * 2);
        LineMargin = new Thickness(
            padding + ((paintAreaWidth - ForgeMetrics.StripeSeparatorLineWidth) / 2),
            finalSize.Height / 2,
            0,
            0);

        return base.ArrangeOverride(finalSize);
    }

    /// <summary>配色。</summary>
    public ForgePalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>是否处于拖拽态（换成 DragAndDrop 那个颜色）。</summary>
    public bool IsDragAndDrop
    {
        get => GetValue(IsDragAndDropProperty);
        set => SetValue(IsDragAndDropProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
        => new(ForgeMetrics.StripeSeparatorWidth, ForgeMetrics.StripeSeparatorHeight);

}
