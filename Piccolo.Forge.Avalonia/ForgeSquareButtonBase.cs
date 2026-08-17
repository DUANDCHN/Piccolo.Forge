using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 条带上方形按钮的共同部分：度量、状态色、悬停。
/// </summary>
/// <remarks>
/// 对应上游的 <c>AbstractSquareStripeButton</c>（<c>SquareStripeButton.kt:62-80</c>）——
/// 工具窗口按钮和「更多」按钮在上游是**兄弟**，都继承它，
/// 而不是后者继承前者。这里保持同样的层级。
/// <para>
/// <b>底座是 Avalonia 的 <see cref="Button"/>。</b>上游那边也是 <c>ActionButton</c>（Swing 的按钮）——
/// 「按下与松开落在同一个按钮上才算一次点击」「键盘空格 / 回车」「按下态」这些框架早就做对了。
/// 自己实现的那一版做出过 <b>侧边栏按钮有时要点两下</b>：它拿"位移有没有超过拖拽死区"当点击判据，
/// 手抖七八个像素就白点一下。
/// </para>
/// <para>
/// <b>背景归模板</b>（REQ-067）：底色与圆角由 <c>ControlTheme</c> 里那层 <c>Border</c> 画，
/// 颜色走 <c>{DynamicResource}</c>，宿主重定义键即可覆盖。图标仍自绘 / 由
/// <see cref="ForgeIconHost"/> 那个槽承载——理由见那个类。
/// </para>
/// <para>
/// ⚠ <b>不要在构造里给 <c>Template</c> 赋值</b>：那是 local value，会盖过 <c>ControlTheme</c>
/// 里的模板，于是主题白写。模板一律写在 XAML 的 <c>ControlTheme</c> 里。
/// </para>
/// </remarks>
public abstract class ForgeSquareButtonBase : Button
{
    /// <summary><see cref="Palette"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgePalette> PaletteProperty =
        AvaloniaProperty.Register<ForgeSquareButtonBase, ForgePalette>(nameof(Palette), ForgePalette.ExpUiDark);

    /// <summary><see cref="IsCompact"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<ForgeSquareButtonBase, bool>(nameof(IsCompact));

    static ForgeSquareButtonBase()
    {
        // 悬停 / 按下两种态归框架管，我们只管"变了就重画"
        AffectsRender<ForgeSquareButtonBase>(
            PaletteProperty, IsCompactProperty, IsLeftStripeProperty, IsPointerOverProperty, IsPressedProperty);
        // 换族会改按钮宽（40 ↔ 37），所以 Palette 也要触发重新测量，不只是重画
        AffectsMeasure<ForgeSquareButtonBase>(
            IsCompactProperty, PaletteProperty, IsLeftStripeProperty, WidthOverrideProperty);

        // 几何（内缩 / 圆角）跟着这三样走 —— 它们是模板里 Border 的 Margin 与 CornerRadius 的来源
        PaletteProperty.Changed.AddClassHandler<ForgeSquareButtonBase>((b, _) => b.OnFamilyOrDensityChanged());
        IsCompactProperty.Changed.AddClassHandler<ForgeSquareButtonBase>((b, _) => b.OnFamilyOrDensityChanged());
        IsLeftStripeProperty.Changed.AddClassHandler<ForgeSquareButtonBase>((b, _) => b.UpdateGeometry());
    }

    /// <summary>族或密度变了：几何要重算，族还要写进伪类（模板按 <c>:islands</c> 选另一套颜色键）。</summary>
    private void OnFamilyOrDensityChanged()
    {
        PseudoClasses.Set(":islands", Palette.Island is not null);
        UpdateGeometry();
    }

    /// <summary>
    /// 背景高亮的矩形：**按钮矩形去掉图标内边距**。
    /// </summary>
    /// <remarks>
    /// 照抄 <c>SquareStripeButtonLook.kt:64-70 · paintBackground</c>——
    /// 它从组件矩形里先后减掉 <c>insets</c> 与 <c>getIconPadding()</c> 再填色。
    /// 两套主题算下来都是 30×30（expUI <c>40-5-5</c>、Islands <c>37-5-2</c>），
    /// Islands 只是把这块整体推向条带外缘。
    /// <para>
    /// 早先这里画的是**整个按钮矩形**，于是 expUI 下高亮是 40×40 而不是 30×30——
    /// 贴着相邻按钮、也贴着条带边缘，看起来比 Rider 胖一圈。
    /// </para>
    /// </remarks>
    /// <param name="cell">
    /// 按钮那一格的矩形。不显名时就是整个 <c>Bounds</c>；
    /// 显名时按钮被名字撑高，这里传的是上面那一格（图标所在的 <see cref="ButtonWidth"/> × <see cref="ButtonHeight"/>）。
    /// </param>
    /// <returns>该填色的矩形。</returns>
    public Rect BackgroundRect(Rect cell)
    {
        var padding = IconPadding;
        var rect = new Rect(
            cell.X + padding.Left,
            cell.Y + padding.Top,
            Math.Max(0, cell.Width - padding.Horizontal),
            Math.Max(0, cell.Height - padding.Vertical));

        // ★ **收进一个居中的正方形**（不显名时）。
        //   上游那两套主题里按钮本来就是方的（40×40 / 37×37），扣掉内边距仍是方的，
        //   所以这一步在上游是恒等变换 —— 一个像素都不动。
        //   宿主把宽度单独收窄之后（ForgeAppearance.StripeButtonWidth）才有分别：
        //   不收成正方形的话，高亮会变成一块长方形板，图标浮在里面像没放正。
        //   ⚠ 显名时**不收**：那一档的底本来就要盖住图标 + 名字整块
        //     （SquareStripeButton.kt:196-198），收成方的会把名字露在底外面。
        if (ShowsName)
        {
            return rect;
        }

        var side = Math.Min(rect.Width, rect.Height);
        return new Rect(
            rect.X + ((rect.Width - side) / 2),
            rect.Y + ((rect.Height - side) / 2),
            side,
            side);
    }

    /// <summary>建一个方形按钮。</summary>
    protected ForgeSquareButtonBase() => Focusable = true;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // 模板里那个图标槽（ControlTheme 给的）；找到就把宿主的图标控件交给它
        _iconHost = e.NameScope.Find<ForgeIconHost>("PART_Icon");
        if (_iconHost is not null)
        {
            // 宿主的控件图标优先；没有就放画 Geometry 的那个子元素
            _iconHost.Icon = _iconControl ?? (GeometryIcon.Icon is null ? null : GeometryIcon);
        }

        UpdateGeometry();
    }

    private ForgeIconHost? _iconHost;
    private Control? _iconControl;

    /// <summary>
    /// 宿主塞进来的图标控件（<c>MaterialIcon</c>、<c>Image</c>…）。
    /// </summary>
    /// <remarks>
    /// 放进模板里那个槽，不直接挂在按钮上 —— 挂上去会被 <c>ApplyTemplate</c> 扫掉。
    /// </remarks>
    /// <param name="control">图标控件；<see langword="null"/> 表示没有。</param>
    protected void SetIconControl(Control? control)
    {
        if (ReferenceEquals(control, _iconControl))
        {
            return;
        }

        _iconControl = control;

        if (_iconHost is not null)
        {
            _iconHost.Icon = control ?? (GeometryIcon.Icon is null ? null : GeometryIcon);
        }

        InvalidateMeasure();
    }

    /// <summary>当前挂着的图标控件。</summary>
    protected Control? IconControl => _iconControl;

    /// <summary>
    /// 画 <see cref="Geometry"/> 图标的那个子元素 —— 宿主没给控件图标时用它。
    /// </summary>
    /// <remarks>
    /// 它和宿主给的图标控件**共用同一个槽**（<see cref="ForgeIconHost"/>），
    /// 所以位置、缩放、居中全都走同一条路（<see cref="IconRect"/>），不会两套算法漂开。
    /// <para>
    /// 搬成子元素的理由见 <see cref="ForgeGeometryIcon"/>：留在 <c>Render</c> 里的话，
    /// 模板里就不能放任何不透明的东西。
    /// </para>
    /// </remarks>
    protected ForgeGeometryIcon GeometryIcon { get; } = new();

    /// <summary>把该画的 Geometry 图标与画刷交给那个子元素；<paramref name="icon"/> 为空则不画。</summary>
    /// <param name="icon">要画的图形。</param>
    /// <param name="brush">用什么画。</param>
    /// <param name="filled">实心还是描边。</param>
    protected void SetGeometryIcon(Geometry? icon, IBrush? brush, bool filled)
    {
        GeometryIcon.Icon = icon;
        GeometryIcon.Brush = brush;
        GeometryIcon.IsFilled = filled;

        // 宿主给了控件图标就以它为准（槽里只放得下一个）
        if (_iconHost is not null && _iconControl is null)
        {
            _iconHost.Icon = icon is null ? null : GeometryIcon;
        }
    }

    /// <summary>
    /// 图标占的那块地（按钮自己的坐标系）。
    /// </summary>
    /// <param name="size">按钮尺寸。</param>
    /// <returns>图标矩形。</returns>
    protected virtual Rect IconRect(Size size) => BackgroundRect(new Rect(size));

    /// <summary>给模板里那个图标槽问位置用的口子。</summary>
    /// <param name="size">槽的尺寸（= 按钮尺寸）。</param>
    /// <returns>图标矩形。</returns>
    internal Rect IconPlacement(Size size) => IconRect(size);

    /// <summary><see cref="BackgroundInsets"/> 的属性定义。</summary>
    public static readonly StyledProperty<Thickness> BackgroundInsetsProperty =
        AvaloniaProperty.Register<ForgeSquareButtonBase, Thickness>(nameof(BackgroundInsets));

    /// <summary><see cref="ArcCornerRadius"/> 的属性定义。</summary>
    public static readonly StyledProperty<CornerRadius> ArcCornerRadiusProperty =
        AvaloniaProperty.Register<ForgeSquareButtonBase, CornerRadius>(nameof(ArcCornerRadius));

    /// <summary>
    /// 背景那层相对按钮要内缩多少 —— 就是 <see cref="IconPadding"/>，换成模板认识的类型。
    /// </summary>
    /// <remarks>
    /// 模板里那层 <c>Border</c> 用它当 <c>Margin</c>，等价于原先
    /// <see cref="BackgroundRect"/> 画的那个"按钮矩形 − 图标内边距"。
    /// <para>
    /// 做成 <c>StyledProperty</c> 而不是普通属性：模板里的 <c>TemplateBinding</c> 只认
    /// Avalonia 属性；而它是**算出来的**（随族 / 密度 / 左右条带 / 显名变），
    /// 所以由 <see cref="UpdateGeometry"/> 在那些输入变化时刷新。
    /// </para>
    /// </remarks>
    public Thickness BackgroundInsets
    {
        get => GetValue(BackgroundInsetsProperty);
        private set => SetValue(BackgroundInsetsProperty, value);
    }

    /// <summary>背景那层的圆角，理由同 <see cref="BackgroundInsets"/>。</summary>
    /// <remarks>值是 <see cref="ArcRadius"/>（上游那个 <c>Arc</c> 是直径，这里已折半）。</remarks>
    public CornerRadius ArcCornerRadius
    {
        get => GetValue(ArcCornerRadiusProperty);
        private set => SetValue(ArcCornerRadiusProperty, value);
    }

    /// <summary>把算出来的几何刷进那两个供模板绑定的属性。</summary>
    /// <remarks>
    /// ⚠ 这里的内边距必须与 <see cref="BackgroundRect"/> **算出同一块地**：
    /// 模板里那层底是靠 <see cref="BackgroundInsets"/> 摆的，而图标的位置走
    /// <see cref="BackgroundRect"/> —— 两处对不上，图标就浮在底里没放正。
    /// 所以"收成居中正方形"那一步两边都要做。
    /// </remarks>
    protected void UpdateGeometry()
    {
        var padding = IconPadding;
        var cell = new Rect(0, 0, ButtonWidth, ButtonHeight);
        var background = BackgroundRect(cell);

        BackgroundInsets = ShowsName
            ? new Thickness(padding.Left, padding.Top, padding.Right, padding.Bottom)
            : new Thickness(
                background.X,
                background.Y,
                Math.Max(0, cell.Width - background.Right),
                Math.Max(0, cell.Height - background.Bottom));

        ArcCornerRadius = new CornerRadius(ArcRadius);
    }

    /// <summary>配色。</summary>
    public ForgePalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>是否紧凑模式（<c>度量与主题键.md</c> §9）。</summary>
    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    /// <summary><see cref="IsLeftStripe"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsLeftStripeProperty =
        AvaloniaProperty.Register<ForgeSquareButtonBase, bool>(nameof(IsLeftStripe), defaultValue: true);

    /// <summary>
    /// 这个按钮在左条带还是右条带。默认左。
    /// </summary>
    /// <remarks>
    /// 内边距**按左右取不同的键**（<c>JBUI.java:1343</c> 把 left/right 拼进键名）。
    /// Islands 下两侧刻意镜像不对称——靠窗口内侧留 2、外侧留 5，
    /// 设错边的结果是图标朝窗口里侧挤，而不是贴着窗口边缘。
    /// </remarks>
    public bool IsLeftStripe
    {
        get => GetValue(IsLeftStripeProperty);
        set => SetValue(IsLeftStripeProperty, value);
    }

    /// <summary>「显示名字」模式——内边距另有一套键，派生类按自己的情况覆盖。</summary>
    protected virtual bool ShowsName => false;

    /// <summary>当前该用的条带按钮度量（随配色族 × 密度）。</summary>
    public ForgeStripeButtonMetrics StripeMetrics
        => ForgeStripeButtonMetrics.ForTheme(Palette.Family, IsCompact);

    /// <summary>按钮宽：expUI 40（紧凑 32），Islands 常规档 37。</summary>
    public double ButtonWidth => double.IsNaN(WidthOverride) ? StripeMetrics.Width : WidthOverride;

    /// <summary>按钮高：40（紧凑 32）。</summary>
    public double ButtonHeight => StripeMetrics.Height;

    /// <summary>图标边长：紧凑 16，否则 20。</summary>
    public double IconSize => StripeMetrics.IconSize;

    /// <summary>图标四周 padding，按左右条带与是否显名取。</summary>
    public ForgeInsets IconPadding => StripeMetrics.IconPadding(IsLeftStripe, ShowsName);

    /// <summary>圆角：紧凑 8，否则 12。</summary>
    public double Arc => ArcOverride ?? (IsCompact ? ForgeCompactMetrics.StripeButtonArc : ForgeMetrics.StripeButtonArc);

    /// <summary>
    /// 画圆角用的**半径** = <see cref="Arc"/> 的一半。
    /// </summary>
    /// <remarks>
    /// ⚠ <see cref="Arc"/> 是上游语义的**直径**（<c>IdeaActionButtonLook.java:36-37</c> 把它当
    /// <c>RoundRectangle2D</c> 的 <c>arcw/arch</c> 用），而 Avalonia 的 <c>RoundedRect</c> 收半径。
    /// 画的时候一律用这个属性，别直接用 <see cref="Arc"/> —— 那样圆角会大一倍。
    /// <para>
    /// 名字**不叫 <c>CornerRadius</c>**：那会隐藏 <c>TemplatedControl</c> 的同名属性，
    /// 编译器只给个警告，而两者语义不同（那个是控件自身边框的圆角）。
    /// </para>
    /// </remarks>
    public double ArcRadius => Arc / 2;

    /// <summary>宿主指定的圆角**直径**；<see langword="null"/> 表示按主题的默认值。</summary>
    public double? ArcOverride { get; set; }

    /// <summary><see cref="WidthOverride"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> WidthOverrideProperty =
        AvaloniaProperty.Register<ForgeSquareButtonBase, double>(nameof(WidthOverride), double.NaN);

    /// <summary>
    /// 宿主指定的按钮**宽度**；<see cref="double.NaN"/> = 按主题的默认值（上游 40 / 紧凑 32）。
    /// </summary>
    /// <remarks>
    /// 给的是**版面取舍**的口子，不是紧凑档开关：紧凑是一整档密度（图标、字号、圆角一起变小），
    /// 而"侧边栏该多宽"常常只想单收这一项。高度与图标尺寸都不跟着变 ——
    /// 图标仍在扣掉内边距后的区域里居中（见 <see cref="ForgeStripeButton.IconRect"/>），
    /// 所以收窄之后图标照样正。
    /// </remarks>
    public double WidthOverride
    {
        get => GetValue(WidthOverrideProperty);
        set => SetValue(WidthOverrideProperty, value);
    }

    /// <summary><see cref="HoverBackgroundBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> HoverBackgroundBrushProperty =
        AvaloniaProperty.Register<ForgeSquareButtonBase, IBrush?>(nameof(HoverBackgroundBrush));

    /// <summary>悬停底，由 <c>ControlTheme</c> 从资源键给（REQ-067）。</summary>
    public IBrush? HoverBackgroundBrush
    {
        get => GetValue(HoverBackgroundBrushProperty);
        set => SetValue(HoverBackgroundBrushProperty, value);
    }

    /// <summary>
    /// 当前该用哪个背景。
    /// </summary>
    /// <remarks>
    /// 优先级照抄 <c>SquareStripeButton.kt:208-219 · getBackgroundColor</c>：
    /// **焦点 &gt; 窗口可见 &gt; 悬停 &gt; 无**。
    /// 注意「窗口可见」压过「悬停」——鼠标划过一个已经打开的工具窗口按钮时，
    /// 显示的是 pressed 背景而不是 hover 背景。
    /// </remarks>
    /// <returns>背景色；都不满足时是 <see langword="null"/>（不画）。</returns>
    public virtual Color? ResolveBackground()
        => IsPointerOver ? Palette.ButtonHoverBackground : null;

    /// <summary>
    /// 当前该用哪个背景画刷 —— 与 <see cref="ResolveBackground"/> 同一套优先级，取的是主题给的画刷。
    /// </summary>
    /// <remarks>
    /// ⚠ <b>底色必须由 <c>Render</c> 画，不能放进模板</b>（REQ-067 实机撞出来的）：
    /// Avalonia 里控件自身的 <c>Render</c> 画在**模板子元素之下**，
    /// 而这些按钮的名字与自绘图标都在 <c>Render</c> 里 ——
    /// 底色一旦做成模板里的 <c>Border</c>，选中态那块不透明的蓝就把名字整个盖住了
    /// （实机症状：<b>选中的按钮只剩图标、底下的字不见了</b>；图标还在是因为它是模板子元素）。
    /// 模板里现在只留放宿主图标控件的槽。
    /// </remarks>
    /// <returns>背景画刷；不画时是 <see langword="null"/>。</returns>
    protected virtual IBrush? ResolveBackgroundBrush()
        => IsPointerOver ? HoverBackgroundBrush ?? Fallback(Palette.ButtonHoverBackground) : null;

    /// <summary>控件还没进可视树时拿不到主题，兜底回配色（两者同源）。</summary>
    /// <param name="color">配色里的颜色。</param>
    /// <returns>画刷。</returns>
    protected static IBrush Fallback(Color color) => new SolidColorBrush(color);

    /// <summary><see cref="BackgroundBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> BackgroundBrushProperty =
        AvaloniaProperty.Register<ForgeSquareButtonBase, IBrush?>(nameof(BackgroundBrush));

    /// <summary>
    /// 当前状态该用的底色 —— <b>模板里那层 <c>Border</c> 绑的就是它</b>。
    /// </summary>
    /// <remarks>
    /// 它是 <see cref="ResolveBackgroundBrush"/> 的结果，由 <see cref="UpdateBackgroundBrush"/>
    /// 在状态变化时刷新。做成属性而不是让模板去调方法，是因为 <c>TemplateBinding</c> 只认属性。
    /// <para>
    /// 之所以能把底色放回模板：这个按钮的 <c>Render</c> 已经不画任何内容了
    /// （图标进了槽、名字是模板里的 <c>TextBlock</c>），不再存在"模板盖住自绘内容"的问题。
    /// </para>
    /// </remarks>
    public IBrush? BackgroundBrush
    {
        get => GetValue(BackgroundBrushProperty);
        private set => SetValue(BackgroundBrushProperty, value);
    }

    /// <summary>状态变了：重算该用哪个底色。</summary>
    protected void UpdateBackgroundBrush() => BackgroundBrush = ResolveBackgroundBrush();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // 悬停 / 按下归框架管，两个状态属性一变就重算底色
        if (change.Property == IsPointerOverProperty
            || change.Property == IsPressedProperty
            || change.Property == HoverBackgroundBrushProperty
            || change.Property == PaletteProperty)
        {
            UpdateBackgroundBrush();
        }
    }

    /// <summary>当前该用哪个前景。</summary>
    /// <returns>前景色。</returns>
    public virtual Color ResolveForeground() => Palette.ButtonForeground;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(ButtonWidth, ButtonHeight);

}
