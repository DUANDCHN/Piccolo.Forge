using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Piccolo.Forge.Model;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 一根条带：上下两组按钮，外加一条内侧边框。
/// </summary>
/// <remarks>
/// 对应 <c>platform/platform-impl/src/com/intellij/toolWindow/ToolWindowToolbar.kt</c>
/// 及其左右两个子类：
/// <list type="bullet">
/// <item>上组在 NORTH、下组在 SOUTH（<c>ToolWindowToolbar.kt:71-75</c>）；</item>
/// <item>左条带右侧 1px 线、右条带左侧 1px 线
/// （<c>ToolWindowLeftToolbar.kt:28</c>、<c>ToolWindowRightToolbar.kt:27</c>）；</item>
/// <item>背景 `ToolWindow.Stripe.background`（<c>ToolWindowToolbar.kt:57</c>）。</item>
/// </list>
/// 落位规则不在这里，在 <see cref="StripePlacementResolver"/>。
/// </remarks>
public class ForgeToolWindowToolbar : TemplatedControl
{
    private readonly StripeItemsPanel _topGroup = new();
    private readonly StripeItemsPanel _bottomGroup = new();
    private readonly DockPanel _root = new() { LastChildFill = false };

    /// <summary><see cref="Palette"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgePalette> PaletteProperty =
        AvaloniaProperty.Register<ForgeToolWindowToolbar, ForgePalette>(nameof(Palette), ForgePalette.ExpUiDark);

    /// <summary><see cref="Side"/> 的属性定义。</summary>
    public static readonly StyledProperty<StripeSide> SideProperty =
        AvaloniaProperty.Register<ForgeToolWindowToolbar, StripeSide>(nameof(Side));

    static ForgeToolWindowToolbar()
    {
        AffectsRender<ForgeToolWindowToolbar>(PaletteProperty, SideProperty);
    }

    /// <summary>建一根条带。</summary>
    public ForgeToolWindowToolbar()
    {
        DockPanel.SetDock(_topGroup, Dock.Top);
        DockPanel.SetDock(_bottomGroup, Dock.Bottom);
        _root.Children.Add(_topGroup);
        _root.Children.Add(_bottomGroup);
        VisualChildren.Add(_root);
        LogicalChildren.Add(_root);
    }

    /// <summary>配色。</summary>
    public ForgePalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>这是左条带还是右条带。</summary>
    public StripeSide Side
    {
        get => GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    /// <summary>上组：放 LEFT / RIGHT 的按钮。</summary>
    public StripeItemsPanel TopGroup => _topGroup;

    /// <summary>下组：放 BOTTOM 的按钮。</summary>
    public StripeItemsPanel BottomGroup => _bottomGroup;

    /// <summary>
    /// 这根条带上有没有任何按钮。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ToolWindowToolbar.kt:151 · hasButtons</c>。
    /// 一根没有可见按钮的条带整根隐藏
    /// （<c>ToolWindowPaneNewButtonManager.kt:76-99 · updateToolStripesVisibility</c>）。
    /// </remarks>
    public bool HasButtons => _topGroup.Children.Count > 0 || _bottomGroup.Children.Count > 0;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        _root.Measure(availableSize);
        var size = _root.DesiredSize;

        // 内侧那条 1px 线占宽
        return new Size(size.Width + 1, size.Height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var contentWidth = Math.Max(0, finalSize.Width - 1);
        var x = Side == StripeSide.Left ? 0 : 1;
        _root.Arrange(new Rect(x, 0, contentWidth, finalSize.Height));
        return finalSize;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        context.FillRectangle(new SolidColorBrush(Palette.StripeBackground), new Rect(Bounds.Size));

        // 左条带的线在右缘，右条带的线在左缘
        var x = Side == StripeSide.Left ? Bounds.Width - 1 : 0;
        context.FillRectangle(
            new SolidColorBrush(Palette.StripeBorder),
            new Rect(x, 0, 1, Bounds.Height));
    }
}
