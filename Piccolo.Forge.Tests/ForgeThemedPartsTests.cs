using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 跟着条带按钮一起模板化的两个小件：分隔线、标题条上的小按钮。
/// </summary>
/// <remarks>
/// 与 <see cref="ForgeStripeButtonThemeTests"/> 同一套判据 —— 颜色来自资源键、状态由伪类选、
/// 族换一套键。它们是"这套做法能不能复制"的第一批证据（REQ-067 的排期依据）。
/// </remarks>
public class ForgeThemedPartsTests
{
    [AvaloniaFact]
    public void 分隔线的颜色按拖拽态与族换键()
    {
        var separator = new ForgeStripeSeparator
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        var window = Host(separator, ThemeVariant.Dark);

        Assert.Equal(ForgePalette.ExpUiDark.StripeSeparator, LineColor(separator));

        separator.IsDragAndDrop = true;
        Relayout(window);
        Assert.Equal(ForgePalette.ExpUiDark.StripeSeparatorDragAndDrop, LineColor(separator));

        separator.Palette = ForgePalette.IslandsDark;
        Relayout(window);
        Assert.Equal(ForgePalette.IslandsDark.StripeSeparatorDragAndDrop, LineColor(separator));

        window.Close();
    }

    /// <summary>
    /// 线的位置照抄原实现：x = padding 5 + (可画宽 − 24) / 2，y = 高 / 2。
    /// </summary>
    /// <remarks>
    /// ⚠ 那个 <c>y</c> 看着像垂直居中，其实**差半像素**（11 高里线落在 5.5，居中该是 5）。
    /// 这条用例把它钉住 —— 搬进模板时最容易"顺手改正"的就是这种地方，
    /// 而改了就与上游 <c>StripeButtonSeparator.kt:31-32</c> 不一致了。
    /// </remarks>
    [AvaloniaFact]
    public void 分隔线的位置照抄上游包括那半个像素()
    {
        var separator = new ForgeStripeSeparator
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        var window = Host(separator, ThemeVariant.Dark);
        var line = Part(separator, "PART_Line");

        // 占位 32×11，线 24×1 → x = 5 + (22 − 24)/2 = 4，y = 5.5
        Assert.Equal(new global::Avalonia.Thickness(4, 5.5, 0, 0), separator.LineMargin);
        Assert.Equal(24, line.Bounds.Width);
        Assert.Equal(1, line.Bounds.Height);

        window.Close();
    }

    [AvaloniaFact]
    public void 标题条按钮的悬停底色与前景都来自主题()
    {
        var button = new ForgeHeaderButton
        {
            Icon = ForgeIcons.Hide,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = 24,
            Height = 24,
        };

        var window = Host(button, ThemeVariant.Dark);

        // 前景（图标色）来自主题
        Assert.Equal(ForgePalette.ExpUiDark.ButtonForeground, ((ISolidColorBrush)button.Foreground!).Color);

        // 悬停底也来自主题（画在 Render 里，见 ForgeSquareButtonBase.ResolveBackgroundBrush）
        Assert.Equal(
            ForgePalette.ExpUiDark.ButtonHoverBackground,
            (button.HoverBackgroundBrush as ISolidColorBrush)?.Color);

        // 换族换一套键
        button.Palette = ForgePalette.IslandsDark;
        Relayout(window);
        Assert.Equal(ForgePalette.IslandsDark.ButtonForeground, ((ISolidColorBrush)button.Foreground!).Color);

        window.Close();
    }

    private static Color? LineColor(ForgeStripeSeparator separator)
        => (Part(separator, "PART_Line").Background as ISolidColorBrush)?.Color;

    private static Border Part(Control control, string name)
        => control.GetVisualDescendants().OfType<Border>().Single(x => x.Name == name);

    private static Window Host(Control content, ThemeVariant variant)
    {
        var window = new Window
        {
            Width = 200,
            Height = 200,
            Content = content,
            RequestedThemeVariant = variant,
        };

        window.Show();
        Relayout(window);
        return window;
    }

    private static void Relayout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
    }
}
