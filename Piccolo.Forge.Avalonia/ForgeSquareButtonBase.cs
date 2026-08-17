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
            IsCompactProperty, PaletteProperty, IsLeftStripeProperty,
            HighlightSizeProperty, HighlightInsetProperty);

        // 几何（内缩 / 圆角）跟着这三样走 —— 它们是模板里 Border 的 Margin 与 CornerRadius 的来源
        PaletteProperty.Changed.AddClassHandler<ForgeSquareButtonBase>((b, _) => b.OnFamilyOrDensityChanged());
        IsCompactProperty.Changed.AddClassHandler<ForgeSquareButtonBase>((b, _) => b.OnFamilyOrDensityChanged());
        IsLeftStripeProperty.Changed.AddClassHandler<ForgeSquareButtonBase>((b, _) => b.UpdateGeometry());
        HighlightSizeProperty.Changed.AddClassHandler<ForgeSquareButtonBase>((b, _) => b.UpdateGeometry());
        HighlightInsetProperty.Changed.AddClassHandler<ForgeSquareButtonBase>((b, _) => b.UpdateGeometry());
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

        // ★ **宿主收窄了按钮时，高亮收成一个在按钮里居中的正方形。**
        //   上游那两套主题的按钮本来就是方的（40×40 / 37×37）；Islands 的图标内边距是
        //   左右不对称的（5 / 2），那是为了把 37 宽的按钮在 40 宽的条带里推向外缘 ——
        //   **按钮与条带不一样宽**时这条才有意义。
        //   宿主用 ForgeAppearance.StripeButtonWidth 收窄之后，按钮就是条带的宽度，
        //   那份不对称失去了由头，留着只会让高亮和图标看着偏一边（实机就是这么被看出来的）。
        //   所以：**宿主没收窄 → 一个像素都不动（照上游）；收窄了 → 收成居中的正方形。**
        //   ⚠ 显名时**不收**：那一档的底本来就要盖住图标 + 名字整块
        //     （SquareStripeButton.kt:196-198），收成方的会把名字露在底外面。
        if (!IsHostSized)
        {
            return rect;
        }

        // 显名那一档只**横向居中**，不收成正方形：底要盖住图标 + 名字整块
        // （SquareStripeButton.kt:196-198），收方了名字会露在底外面。
        // 但左右仍然要居中 —— 实机量到的是 bg 中心 30.5、按钮中心 28.5，偏 2px，
        // 正是 Islands 那份 6/2 的不对称在这条路上没被处理。
        if (ShowsName)
        {
            return new Rect(
                cell.X + ((cell.Width - rect.Width) / 2),
                rect.Y,
                rect.Width,
                rect.Height);
        }

        // 宿主给了边长就用它（居中由 边距 + 方底 + 边距 这条算式保证是整数）
        var side = HighlightSize;

        return new Rect(
            cell.X + ((cell.Width - side) / 2),
            cell.Y + ((cell.Height - side) / 2),
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

    /// <summary><see cref="BackgroundSize"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> BackgroundSizeProperty =
        AvaloniaProperty.Register<ForgeSquareButtonBase, double>(nameof(BackgroundSize), double.NaN);

    /// <summary>
    /// 高亮底的**边长**；<see cref="double.NaN"/> = 不指定（按 <see cref="BackgroundInsets"/> 铺满）。
    /// </summary>
    /// <remarks>
    /// 只有"宿主收窄了按钮"这一档会给值 —— 那时底要收成一个在按钮里**居中的正方形**，
    /// 见 <see cref="BackgroundRect"/> 的说明。给边长而不是给 Margin，是因为 Margin
    /// 相对的是实际排到的尺寸，与度量里的 ButtonWidth/Height 不一定相等。
    /// </remarks>
    public double BackgroundSize
    {
        get => GetValue(BackgroundSizeProperty);
        private set => SetValue(BackgroundSizeProperty, value);
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

        // ⚠ 方底**不能靠 Margin 拉伸出来**：Margin 相对的是**实际排到的尺寸**，
        //   而这里的算式用的是度量里的 ButtonWidth/Height。两者一旦不等
        //   （Islands 的按钮度量 37 高、条带却按 40 排），拉出来就不是正方形（实机量到 27×28）。
        //   所以方底那一档改成：**给显式边长 + 居中 + 不留边距**。
        //   边距一旦还在，居中就是"在扣掉不对称内边距之后的那块地里居中"——
        //   Islands 的 5/2 会把它推偏 1.5px，正是实机看到的"图标偏一边"。
        var square = !ShowsName && IsHostSized;

        // 宿主收窄了按钮时，左右一律**对称**：Islands 那份 5/2（显名档 6/2）是为了
        // 把窄按钮在宽条带里推向外缘，按钮与条带一样宽之后它只会让人看着偏一边。
        var horizontal = !IsHostSized
            ? (padding.Left, padding.Right)
            : ((cell.Width - background.Width) / 2, (cell.Width - background.Width) / 2);

        BackgroundInsets = square
            ? default
            : new Thickness(horizontal.Item1, padding.Top, horizontal.Item2, padding.Bottom);
        BackgroundSize = square ? HighlightSize : double.NaN;

        // 模板据此换对齐：方底那一档居中，其余照旧铺满
        PseudoClasses.Set(":square-background", square);

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
    public double ButtonWidth => IsHostSized ? (HighlightInset * 2) + HighlightSize : StripeMetrics.Width;

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

    /// <summary><see cref="HighlightSize"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> HighlightSizeProperty =
        AvaloniaProperty.Register<ForgeSquareButtonBase, double>(nameof(HighlightSize), double.NaN);

    /// <summary><see cref="HighlightInset"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> HighlightInsetProperty =
        AvaloniaProperty.Register<ForgeSquareButtonBase, double>(nameof(HighlightInset), double.NaN);

    /// <summary>
    /// 宿主指定的**高亮底**边长（那块方底）；<see cref="double.NaN"/> = 按主题算（按钮 − 图标内边距）。
    /// </summary>
    /// <remarks>与 <see cref="HighlightInset"/> **成对给**，见那里。图标不受它影响，仍是主题里的尺寸。</remarks>
    public double HighlightSize
    {
        get => GetValue(HighlightSizeProperty);
        set => SetValue(HighlightSizeProperty, value);
    }

    /// <summary>
    /// 宿主指定的**高亮底左右边距**；<see cref="double.NaN"/> = 不指定（宽度走主题的按钮宽）。
    /// </summary>
    /// <remarks>
    /// ★ 这是"侧边栏该多宽"的**正确问法**（用户定的做法）：
    /// 别去钉条带宽度，而是钉<b>那块方底多大、两侧留多少</b>，宽度让它撑开
    /// —— <c>宽 = 边距 + 方底 + 边距</c>。图标仍按主题的尺寸，在方底里居中。
    /// <para>
    /// 这么定之后<b>居中是构造上成立的</b>：两侧同一个数，不存在"差 1px 该给谁"的问题。
    /// 先前直接钉宽度（35 / 57）就踩了这个：宽度减图标是奇数，居中落在半像素上，
    /// 取整后左右差 1px，实机看着就是图标偏一边。
    /// </para>
    /// </remarks>
    public double HighlightInset
    {
        get => GetValue(HighlightInsetProperty);
        set => SetValue(HighlightInsetProperty, value);
    }

    /// <summary>宿主是不是自己定了图标尺寸与边距（那时宽度由它们算出来）。</summary>
    protected bool IsHostSized => !double.IsNaN(HighlightInset) && !double.IsNaN(HighlightSize);

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
