using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
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
/// <b>宿主可以直接 new 它来塞自己的按钮</b>（<c>ForgeToolWindowFrame.HeaderButtonsExtension</c> /
/// <c>ForgeEditorArea.TabActionsExtension</c>），也可以塞任意别的 <see cref="Control"/> ——
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
    }

    /// <summary>建一个标题条按钮。</summary>
    public ForgeHeaderButton()
    {
        Focusable = false;

        // 换掉 Button 自带的模板：这个按钮只有自绘内容，模板里放一个空壳就行。
        // ⚠ 不能返回 null —— Avalonia 应用模板时会当场 NRE。
        Template = new FuncControlTemplate((_, _) => new Panel());
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

        if (IsPointerOver || IsPressed)
        {
            context.DrawRectangle(
                new SolidColorBrush(Palette.ButtonHoverBackground),
                null,
                new RoundedRect(new Rect(Bounds.Size), ForgeMetrics.HeaderButtonArc));
        }

        if (Icon is { } icon)
        {
            var size = ForgeMetrics.HeaderButtonIconSize;
            var rect = new Rect(
                (Bounds.Width - size) / 2,
                (Bounds.Height - size) / 2,
                size,
                size);

            // 圆点类图标是实心的（三个小方块），描边画出来会变成三个小方框；
            // 线条类（隐藏那条横线）填充与描边一致，所以统一走填充。
            ForgeIcons.Draw(context, icon, rect, new SolidColorBrush(Palette.ButtonForeground), filled: IsFilledIcon);
        }
    }

}
