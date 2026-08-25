using System.Linq;
using Avalonia.Controls;
using AvaloniaPath = Avalonia.Controls.Shapes.Path;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 折叠面板的展开标记摆哪一侧（<see cref="ForgeExpander.ChevronPlacementProperty"/>）。
/// </summary>
/// <remarks>
/// 两件事各一条：默认在左；设成右之后标记真的换到了行尾那一列。
/// <para>
/// 还有一条盯**继承** —— 这个属性的用法是"设在容器上，整片面板跟着变"，
/// 不继承的话宿主得逐个面板去设，那正是它想省掉的事。
/// </para>
/// </remarks>
public class ExpanderChevronPlacementTests
{
    private static AvaloniaPath Chevron(Expander expander)
        => expander.GetVisualDescendants().OfType<AvaloniaPath>().Single(p => p.Name == "PART_Chevron");

    private static Expander Show(Control root, Expander expander)
    {
        var window = new Window { Content = root, Width = 300, Height = 200 };
        window.Show();
        window.UpdateLayout();
        return expander;
    }

    [AvaloniaFact]
    public void Chevron_sits_on_the_left_by_default()
    {
        var expander = new Expander { Header = "采集控制", Content = new TextBlock { Text = "帧率" } };
        Show(expander, expander);

        Assert.Equal(0, Grid.GetColumn(Chevron(expander)));
    }

    [AvaloniaFact]
    public void Chevron_moves_to_the_row_end_when_placed_right()
    {
        var expander = new Expander { Header = "采集控制", Content = new TextBlock { Text = "帧率" } };
        ForgeExpander.SetChevronPlacement(expander, ForgeChevronPlacement.Right);
        Show(expander, expander);

        Assert.Equal(2, Grid.GetColumn(Chevron(expander)));
    }

    [AvaloniaFact]
    public void Placement_inherits_from_an_ancestor_container()
    {
        var a = new Expander { Header = "采集控制", Content = new TextBlock { Text = "帧率" } };
        var b = new Expander { Header = "曝光控制", Content = new TextBlock { Text = "曝光时间" } };
        var panel = new StackPanel();
        panel.Children.Add(a);
        panel.Children.Add(b);
        ForgeExpander.SetChevronPlacement(panel, ForgeChevronPlacement.Right);

        Show(panel, a);

        Assert.Equal(2, Grid.GetColumn(Chevron(a)));
        Assert.Equal(2, Grid.GetColumn(Chevron(b)));
    }
}
