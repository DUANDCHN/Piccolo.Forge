using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 标签选中态**两族画法不同**：经典（expUI）画下划线，Islands 画药丸底。
/// </summary>
/// <remarks>
/// 这条是对着 Rider 实机订正出来的（2026-08-25）。此前两族共用块底，依据是一句
/// "下划线是老 UI 的做法、新 UI 已不用" —— 那句话是错的：Rider 经典档现在画的就是下划线。
/// <para>
/// 所以这里断言的是**两族有别**，而不是某一族长什么样：谁把它们改回同一种画法，这条就红。
/// </para>
/// </remarks>
public class TabSelectionStyleTests
{
    private static (Border underline, Border root) SelectedParts()
    {
        var tabs = new TabControl
        {
            Items =
            {
                new TabItem { Header = "构建", Content = new TextBlock() },
                new TabItem { Header = "构建输出", Content = new TextBlock() },
            },
        };
        var window = new Window { Content = tabs, Width = 400, Height = 300 };
        window.Show();
        window.UpdateLayout();

        var selected = tabs.GetVisualDescendants().OfType<TabItem>().First(t => t.IsSelected);
        var underline = selected.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_Underline");
        var root = selected.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_Root");
        return (underline, root);
    }

    [AvaloniaFact]
    public void Classic_underlines_the_selected_tab()
    {
        using var scope = ForgeThemeScope.Use(ForgeThemeFamily.ExpUi);
        var (underline, root) = SelectedParts();

        Assert.True(underline.Bounds.Height > 0, "经典档选中的标签应该有一条下划线");
        Assert.NotEqual(0, ((ISolidColorBrush)underline.Background!).Color.A);
        // 底不画块：选中的表达全交给那条线
        Assert.Equal(0, ((ISolidColorBrush)root.Background!).Color.A);
    }

    [AvaloniaFact]
    public void Islands_fills_the_selected_tab_instead()
    {
        using var scope = ForgeThemeScope.Use(ForgeThemeFamily.Islands);
        var (underline, root) = SelectedParts();

        Assert.Equal(0, underline.Bounds.Height);
        Assert.NotEqual(0, ((ISolidColorBrush)root.Background!).Color.A);
    }
}
