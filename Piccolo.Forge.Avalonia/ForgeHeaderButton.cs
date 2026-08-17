using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 标题条 / 标签条右侧那排小按钮里的一个（⋮ 选项、— 隐藏…）。
/// </summary>
/// <remarks>
/// 对应上游标题条右侧那条 <c>ActionToolbar</c> 里的一个 <c>ActionButton</c>
/// （<c>ToolWindowHeader.kt:119-202</c>），按新 UI 的最小按钮尺寸 24×24 排，图标 16。
/// <para>
/// 与 <see cref="ForgeStripeButton"/> 的区别：那个是条带上的大方块（40×40、带选中态与名字），
/// 这个只是一个动作按钮 —— 只有悬停/按下两种底，没有"这个窗口开着"的状态。
/// </para>
/// <para>
/// 底座是 <see cref="Button"/>：点击、键盘、按下态都归框架（理由同
/// <see cref="ForgeSquareButtonBase"/>）。模板换成空的，只留自绘。
/// </para>
/// <para>
/// <b>宿主可以直接 new 它来塞自己的按钮</b>（<c>ForgeControl.HeaderButtonsExtension</c> /
/// <c>ForgeEditorControl.TabActionsExtension</c>），也可以塞任意别的 <see cref="Control"/> ——
/// 库不规定宿主的按钮长什么样，只保证位置和排布。
/// </para>
/// </remarks>
public class ForgeHeaderButton : Button
{
    /// <summary><see cref="Palette"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgePalette> PaletteProperty =
        AvaloniaProperty.Register<ForgeHeaderButton, ForgePalette>(nameof(Palette), ForgePalette.ExpUiDark);

    /// <summary><see cref="Icon"/> 的属性定义。</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ForgeHeaderButton, Geometry?>(nameof(Icon));

    static ForgeHeaderButton()
    {
        AffectsRender<ForgeHeaderButton>(PaletteProperty, IconProperty, IsPointerOverProperty, IsPressedProperty);

        // 族写进伪类，模板据此换一套颜色键（REQ-067）
        PaletteProperty.Changed.AddClassHandler<ForgeHeaderButton>(
            (x, _) => x.PseudoClasses.Set(":islands", x.Palette.Island is not null));
        AffectsRender<ForgeHeaderButton>(HoverBackgroundBrushProperty);
    }

    /// <summary>建一个标题条按钮。</summary>
    /// <remarks>
    /// ⚠ <b>这里曾经在构造里赋 <c>Template</c></b>（一个空 <c>Panel</c>，只为盖掉 Button 自带的模板）。
    /// 那是 local value，**会盖过 <c>ControlTheme</c> 里的模板** —— REQ-067 把底色搬进模板之后，
    /// 症状是"主题写了但一点不生效"，而且不报错。模板现在一律写在
    /// <c>Themes/ForgeHeaderButton.axaml</c> 的 <c>ControlTheme</c> 里。
    /// </remarks>
    public ForgeHeaderButton() => Focusable = false;

    /// <summary><see cref="HoverBackgroundBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> HoverBackgroundBrushProperty =
        AvaloniaProperty.Register<ForgeHeaderButton, IBrush?>(nameof(HoverBackgroundBrush));

    /// <summary>悬停 / 按下时的底，由 <c>ControlTheme</c> 从资源键给（REQ-067）。</summary>
    public IBrush? HoverBackgroundBrush
    {
        get => GetValue(HoverBackgroundBrushProperty);
        set => SetValue(HoverBackgroundBrushProperty, value);
    }

    /// <summary>配色。</summary>
    public ForgePalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>画什么图标（<see cref="ForgeIcons"/> 里那套）。</summary>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>图标是实心的还是描边的；圆点类实心、线条类描边。</summary>
    /// <remarks>默认按 <see cref="ForgeIcons.MoreVertical"/> 那类实心走；线条图标（如
    /// <see cref="ForgeIcons.Hide"/>）设 <see langword="false"/>。</remarks>
    public bool IsFilledIcon { get; set; } = true;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
        => new(ForgeMetrics.HeaderButtonSize, ForgeMetrics.HeaderButtonSize);

    /// <inheritdoc />
    /// <remarks>
    /// ⚠ 整块都要画上东西（哪怕透明）：Avalonia 的命中测试基于渲染内容，
    /// 什么都不画的控件收不到指针事件 —— 同 <see cref="ForgeDockResizer.Render"/> 记过的那条。
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        ForgeHitArea.Fill(context, Bounds.Size);

        // 底色与图标都在模板里（Border#PART_Background / ForgeGeometryIcon），
        // 这里只剩铺命中区 —— 不铺的话没画东西的地方收不到鼠标。
    }

    /// <summary>把图标交给模板里那个画 Geometry 的子元素。</summary>
    /// <remarks>
    /// 圆点类图标是实心的（三个小方块），描边画出来会变成三个小方框；
    /// 线条类（隐藏那条横线）填充与描边一致，所以那类统一走填充。
    /// <para>
    /// 前景由模板给（<c>ControlTheme</c> 从资源键取）；控件还没进树时兜底回配色，
    /// 两者同源所以看不出差别（见 <c>ThemeResourceSpikeTests</c> 里"树外取不到"那条）。
    /// </para>
    /// </remarks>
    private void SyncIcon()
    {
        _geometryIcon.Icon = Icon;
        _geometryIcon.Brush = Foreground ?? new SolidColorBrush(Palette.ButtonForeground);
        _geometryIcon.IsFilled = IsFilledIcon;
    }

    private readonly ForgeGeometryIcon _geometryIcon = new();

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // 模板里那个图标位：16×16 居中（HeaderButtonIconSize），由模板自己摆
        if (e.NameScope.Find<ContentPresenter>("PART_Icon") is { } slot)
        {
            slot.Content = _geometryIcon;
        }

        SyncIcon();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconProperty
            || change.Property == ForegroundProperty
            || change.Property == PaletteProperty)
        {
            SyncIcon();
        }
    }

}
