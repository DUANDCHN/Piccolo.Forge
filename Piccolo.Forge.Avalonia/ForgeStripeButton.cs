using Avalonia;
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
        AffectsMeasure<ForgeStripeButton>(ShowNameProperty, StripeWidthProperty, TitleProperty);

        // 两个状态写进伪类，模板据此选底色（见 Themes/ForgeStripeButton.axaml）。
        // 用伪类而不是样式类：它们是控件自己的状态，不该让宿主用 Classes 去改。
        IsToolWindowVisibleProperty.Changed.AddClassHandler<ForgeStripeButton>(
            (b, e) => b.PseudoClasses.Set(":toolwindow-visible", e.GetNewValue<bool>()));
        IsToolWindowActiveProperty.Changed.AddClassHandler<ForgeStripeButton>(
            (b, e) => b.PseudoClasses.Set(":toolwindow-active", e.GetNewValue<bool>()));

        // 显名会换一整套内边距键，几何跟着变
        ShowNameProperty.Changed.AddClassHandler<ForgeStripeButton>((b, _) => b.UpdateGeometry());
    }

    /// <summary>对应的工具窗口当前是否可见。</summary>
    public bool IsToolWindowVisible
    {
        get => GetValue(IsToolWindowVisibleProperty);
        set => SetValue(IsToolWindowVisibleProperty, value);
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

    /// <summary>工具窗口的名字，<see cref="ShowName"/> 打开时画在图标下面。</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

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

    /// <summary>按当前密度算出的字号（<see cref="NameFontSize"/> 的来源）。</summary>
    private double ComputeNameFontSize() => Math.Max(8, IconSize * 0.5);

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

    /// <summary>把名字的文本、位置、行高刷进那三个供模板绑定的属性。</summary>
    private void SyncName()
    {
        var lines = SplitTitle(Title);
        NameText = ShowName && lines.Count > 0 ? string.Join('\n', lines) : null;
        NameFontSize = ComputeNameFontSize();
        NameLineHeight = NameFontSize * 1.4;

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

        // 高度 = 基础高 + 间距 + 行数 × 行高（SquareStripeButton.kt:320-327）
        var lines = SplitTitle(Title).Count;
        // ⚠ 这里用**算出来的**字号，不用 NameFontSize 那个属性：属性靠 SyncName 刷，
        //   而测量可能发生在它之前（控件刚建好、还没收到任何属性变更）—— 那时属性是 0，
        //   高度会少掉名字那一截。
        var fontSize = ComputeNameFontSize();
        var height = ButtonHeight + (lines > 0 ? ForgeMetrics.StripeIconLabelGap + (lines * fontSize * 1.4) : 0);
        var width = StripeWidth > 0 ? StripeWidth : ButtonWidth;

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
    /// ★ 前景**由模板给**（REQ-067）：<c>ControlTheme</c> 按 <c>:toolwindow-active</c> / <c>:islands</c>
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
    protected override Rect IconRect(Size size)
    {
        // 显名时按钮被名字撑高，图标只在上面那一格里排
        var cell = ShowName
            ? new Rect((size.Width - ButtonWidth) / 2, 0, ButtonWidth, ButtonHeight)
            : new Rect(size);

        var area = BackgroundRect(cell);
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
        PaletteProperty.Changed.AddClassHandler<ForgeStripeSeparator>(
            (x, _) => x.PseudoClasses.Set(":islands", x.Palette.Island is not null));
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
