using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 拆分按钮的**无边框档**（<c>.tertiary</c>，工具栏用）。
/// </summary>
/// <remarks>
/// 这一档没有外框把两半圈起来，所以背板必须**整块由外壳出**：
/// 两半各画各的方角背板时，看不出是一个控件，圆角也与同排图标钮对不上。
/// </remarks>
public class ForgeSplitButtonTests
{
    private static (Window w, SplitButton sb) Host(params string[] classes)
    {
        var sb = new SplitButton { Content = "保存" };
        foreach (var c in classes) sb.Classes.Add(c);
        var w = new Window { Width = 300, Height = 120, Content = sb };
        w.Show();
        Dispatcher.UIThread.RunJobs();
        return (w, sb);
    }

    // ⚠ 取 First 不取 Single：两半自己的模板里也有 PART_Root / PART_Overlay，
    // 外壳那份在树上最靠前。
    private static T Part<T>(SplitButton sb, string name) where T : Control =>
        sb.GetVisualDescendants().OfType<T>().First(x => x.Name == name);

    [AvaloniaFact]
    public void 无边框档_底与分隔线都不画()
    {
        var (w, sb) = Host("tertiary");
        Assert.Equal(Brushes.Transparent, Part<Border>(sb, "PART_Root").Background);

        // 分隔线**占位常驻、常态透明**：用 IsVisible 切显隐会改控件总宽，
        // 悬停一瞬后排按钮全体挪一格（宿主实测到的布局跳动）。
        var sep = Part<Border>(sb, "PART_Separator");
        Assert.True(sep.IsVisible);
        Assert.Equal(Brushes.Transparent, sep.Background);
        w.Close();
    }

    [AvaloniaFact]
    public void 无边框档_圆角与普通按钮同档()
    {
        var (w, sb) = Host("tertiary");
        var expected = (CornerRadius)sb.FindResource("Forge.Button.CornerRadius")!;

        // 背板画在 PART_Root 上，所以圆角要看它 —— 两半自己不画背板（见下一条）。
        Assert.Equal(expected, Part<Border>(sb, "PART_Root").CornerRadius);
        w.Close();
    }

    [AvaloniaFact]
    public void 无边框档_两半交出叠加权()
    {
        var (w, sb) = Host("tertiary");
        var noOverlay = sb.FindResource("Forge.SplitButtonPart.NoOverlay");

        // 两半用的必须是"自己不画叠加"那份主题，否则悬停会出现两块深浅不一的背板。
        Assert.Same(noOverlay, Part<Button>(sb, "PART_PrimaryButton").Theme);
        Assert.Same(noOverlay, Part<Button>(sb, "PART_SecondaryButton").Theme);
        w.Close();
    }

    [AvaloniaFact]
    public void 无边框档_两半不撑高度()
    {
        var (w, sb) = Host("tertiary");

        // 两半沿用带文字按钮的 MinHeight 时，整块背板会比同排图标钮高一截。
        Assert.Equal(0, Part<Button>(sb, "PART_PrimaryButton").MinHeight);
        Assert.Equal(0, Part<Button>(sb, "PART_SecondaryButton").MinHeight);
        w.Close();
    }

    [AvaloniaFact]
    public void 有边框档_照旧由两半各自反馈()
    {
        var (w, sb) = Host();
        var plain = sb.FindResource("Forge.SplitButtonPart");

        Assert.Same(plain, Part<Button>(sb, "PART_PrimaryButton").Theme);
        Assert.True(Part<Border>(sb, "PART_Separator").IsVisible);
        w.Close();
    }
}
