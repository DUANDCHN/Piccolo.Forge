using Avalonia;
using Avalonia.Media;
using Piccolo.Forge.Model;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 条带上的「更多」按钮。
/// </summary>
/// <remarks>
/// 对应 <c>platform/platform-impl/src/com/intellij/toolWindow/MoreSquareStripeButton.kt</c>。
/// **同一时刻只有一根条带显示它**（见 <see cref="Side"/>），
/// 且没有可显示的工具窗口时整个隐藏（<c>:124-135</c>）。
/// </remarks>
public class ForgeMoreButton : ForgeSquareButtonBase
{
    /// <summary><see cref="Side"/> 的属性定义。</summary>
    public static readonly StyledProperty<StripeSide> SideProperty =
        AvaloniaProperty.Register<ForgeMoreButton, StripeSide>(nameof(Side));

    /// <summary><see cref="ActiveSide"/> 的属性定义。</summary>
    public static readonly StyledProperty<StripeSide> ActiveSideProperty =
        AvaloniaProperty.Register<ForgeMoreButton, StripeSide>(nameof(ActiveSide));

    /// <summary><see cref="HasHiddenToolWindows"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> HasHiddenToolWindowsProperty =
        AvaloniaProperty.Register<ForgeMoreButton, bool>(nameof(HasHiddenToolWindows));

    static ForgeMoreButton()
    {
        AffectsRender<ForgeMoreButton>(SideProperty, ActiveSideProperty, HasHiddenToolWindowsProperty);
    }

    /// <summary>这个按钮长在哪根条带上。</summary>
    public StripeSide Side
    {
        get => GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    /// <summary>
    /// 当前「更多」按钮该显示在哪一边。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ToolWindowManagerState.moreButton</c>，默认 LEFT
    /// （<c>ToolWindowManagerState.kt:60</c>）。
    /// </remarks>
    public StripeSide ActiveSide
    {
        get => GetValue(ActiveSideProperty);
        set => SetValue(ActiveSideProperty, value);
    }

    /// <summary>有没有能从弹窗里挑的工具窗口。</summary>
    public bool HasHiddenToolWindows
    {
        get => GetValue(HasHiddenToolWindowsProperty);
        set => SetValue(HasHiddenToolWindowsProperty, value);
    }

    /// <summary>
    /// 这个按钮现在该不该显示。
    /// </summary>
    /// <remarks>
    /// 对应 <c>MoreSquareStripeButton.kt:77-79</c> 与 <c>:134</c>：
    /// **边要对得上，且有东西可挑**。
    /// </remarks>
    public bool IsAvailable => Side == ActiveSide && HasHiddenToolWindows;

    /// <summary>
    /// 弹窗该出现在哪。
    /// </summary>
    /// <remarks>
    /// 对应 <c>MoreSquareStripeButton.kt:118-121</c>：
    /// 左条带弹在 <c>x = 条带宽</c>，右条带弹在 <c>x = -最小宽度</c>，y 对齐按钮自身。
    /// </remarks>
    /// <param name="side">哪根条带。</param>
    /// <param name="stripeWidth">条带宽度。</param>
    /// <param name="buttonY">按钮的纵坐标。</param>
    /// <returns>相对条带的弹出位置。</returns>
    public static Point ComputePopupOffset(StripeSide side, double stripeWidth, double buttonY)
        => new(side == StripeSide.Left ? stripeWidth : -ForgeMetrics.MorePopupMinWidth, buttonY);

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
        => IsAvailable ? base.MeasureOverride(availableSize) : default;

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (!IsAvailable)
        {
            return;
        }

        if (ResolveBackground() is { } color)
        {
            context.DrawRectangle(new SolidColorBrush(color), null, new RoundedRect(new Rect(Bounds.Size), Arc));
        }

        // 占位画法：三个点，对应 AllIcons.Actions.MoreHorizontal（:91-96）
        var brush = new SolidColorBrush(ResolveForeground());
        var dot = Math.Max(1, IconSize / 10);
        var gap = dot * 2;
        var totalWidth = (dot * 3) + (gap * 2);
        var x = (Bounds.Width - totalWidth) / 2;
        var y = (Bounds.Height - dot) / 2;

        for (var i = 0; i < 3; i++)
        {
            context.FillRectangle(brush, new Rect(x + (i * (dot + gap)), y, dot, dot));
        }
    }
}
