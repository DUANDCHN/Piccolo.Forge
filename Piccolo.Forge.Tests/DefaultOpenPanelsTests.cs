using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Mvvm.Controls;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 宿主「常驻面板默认打开」这件事的前提：一格只能有一个可见。
public class DefaultOpenPanelsTests
{
    private sealed class Tool(string id) : ToolWindowPanel(id);

    /// 宿主的典型登记表：一格可能声明了不止一个常驻面板。
    private static readonly (string Id, ToolWindowAnchor Anchor, bool IsSplit, bool Resident)[] Definitions =
    [
        ("CamList", ToolWindowAnchor.Left, false, true),
        ("ImageLibrary", ToolWindowAnchor.Left, false, true),     // 与 CamList 同格
        ("CamParam", ToolWindowAnchor.Right, false, true),
        ("DisplayLog", ToolWindowAnchor.Bottom, false, true),
        ("Histogram", ToolWindowAnchor.Bottom, true, false),      // 非常驻
    ];

    [Fact]
    public void Opening_every_resident_panel_would_displace_same_slot_ones()
    {
        // 这就是「不能把所有常驻面板都打开」的原因
        var shell = Build();

        foreach (var (id, _, _, resident) in Definitions.Where(d => d.Resident))
        {
            shell.Open(id);
        }

        // 同格的两个：后开的把先开的挤掉了，留下的是**最后声明**的那个
        Assert.False(shell.Find("CamList")!.IsOpen);
        Assert.True(shell.Find("ImageLibrary")!.IsOpen);
    }

    [Fact]
    public void Opening_the_first_resident_of_each_slot_gives_one_per_edge()
    {
        // 正确做法：每格取第一个常驻面板，声明顺序即优先级
        var shell = Build();

        var wanted = Definitions
            .Where(d => d.Resident)
            .GroupBy(d => (d.Anchor, d.IsSplit))
            .Select(g => g.First().Id)
            .ToList();

        foreach (var id in wanted)
        {
            shell.Open(id);
        }

        Assert.Equal(["CamList", "CamParam", "DisplayLog"], wanted);
        Assert.True(shell.Find("CamList")!.IsOpen);
        Assert.True(shell.Find("CamParam")!.IsOpen);
        Assert.True(shell.Find("DisplayLog")!.IsOpen);
        Assert.False(shell.Find("ImageLibrary")!.IsOpen);   // 同格的第二个仍然关着
    }

    [Fact]
    public void Every_edge_ends_up_with_exactly_one_visible_panel()
    {
        var shell = Build();
        foreach (var id in new[] { "CamList", "CamParam", "DisplayLog" })
        {
            shell.Open(id);
        }

        Assert.NotNull(shell.OpenOn(ToolWindowAnchor.Left));
        Assert.NotNull(shell.OpenOn(ToolWindowAnchor.Right));
        Assert.NotNull(shell.OpenOn(ToolWindowAnchor.Bottom));

        // 下边的另一半没开 —— 那是 isSplit=true 的另一格
        Assert.Null(shell.OpenOn(ToolWindowAnchor.Bottom, isSplit: true));
    }

    [Fact]
    public void Both_halves_of_the_bottom_can_be_open_at_once()
    {
        // 「一格一个」不是「一条边一个」
        var shell = Build();

        shell.Open("DisplayLog");
        shell.Open("Histogram");

        Assert.True(shell.Find("DisplayLog")!.IsOpen);
        Assert.True(shell.Find("Histogram")!.IsOpen);
    }

    private static ToolWindowShellViewModel Build()
    {
        var shell = new ToolWindowShellViewModel();
        foreach (var (id, anchor, isSplit, _) in Definitions)
        {
            shell.Add(new Tool(id), anchor, isSplit);
        }

        return shell;
    }
}
