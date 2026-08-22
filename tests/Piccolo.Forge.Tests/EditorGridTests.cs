using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 程序化宫格（<see cref="EditorArea.ArrangeGrid"/> / <see cref="EditorArea.CollapseToSingleGroup"/>）。
/// </summary>
/// <remarks>
/// 这组测试盯的是宿主拿两个原语「敲」格子时够不着的那两样东西：
/// <b>比例</b>（敲出来的每一刀都是 0.5，三格于是 50%/25%/25%）与
/// <b>收回来的页序</b>。所以断言的是**每格实际占多大**，不是树长什么样。
/// </remarks>
public class EditorGridTests
{
    private static EditorArea Area(int tabCount)
    {
        var area = new EditorArea();
        for (var i = 0; i < tabCount; i++)
        {
            area.Open(new EditorTab($"d{i}", $"d{i}"));
        }

        return area;
    }

    private static List<string> Ids(int count) => Enumerable.Range(0, count).Select(i => $"d{i}").ToList();

    /// <summary>每一格实际占整块编辑区的多少（宽 × 高），按屏幕次序。</summary>
    private static List<(EditorGroup Group, double Width, double Height)> Cells(
        EditorNode node, double width = 1, double height = 1)
    {
        if (node is not EditorSplit split)
        {
            return [((EditorGroup)node, width, height)];
        }

        var p = split.Proportion;
        return split.IsVertical
            ? [.. Cells(split.First, width, height * p), .. Cells(split.Second, width, height * (1 - p))]
            : [.. Cells(split.First, width * p, height), .. Cells(split.Second, width * (1 - p), height)];
    }

    [Theory]
    [InlineData(2, 1, 2)]   // 1×2：左右各半
    [InlineData(3, 1, 3)]   // 一行三格 —— 曾经是 50%/25%/25%
    [InlineData(4, 2, 2)]
    [InlineData(6, 2, 3)]
    [InlineData(9, 3, 3)]
    public void 宫格每一格等大(int tabCount, int rows, int cols)
    {
        var area = Area(tabCount);
        Assert.True(area.ArrangeGrid(Ids(tabCount), rows, cols));

        var cells = Cells(area.Root);
        Assert.Equal(tabCount, cells.Count);

        foreach (var cell in cells)
        {
            Assert.Equal(1d / cols, cell.Width, 3);
            Assert.Equal(1d / rows, cell.Height, 3);
        }
    }

    [Fact]
    public void 行主序按给的顺序进格子()
    {
        var area = Area(4);
        area.ArrangeGrid(Ids(4), 2, 2);

        var cells = Cells(area.Root);
        Assert.Equal(["d0", "d1", "d2", "d3"], cells.Select(c => c.Group.Tabs.Single().Id));
    }

    [Fact]
    public void 格数按页数封顶且行高仍均分()
    {
        // 4 页点 3×3：只建 4 格（3 + 1），两行**等高**
        var area = Area(4);
        area.ArrangeGrid(Ids(4), 3, 3);

        var cells = Cells(area.Root);
        Assert.Equal(4, cells.Count);
        Assert.All(cells, c => Assert.Equal(0.5, c.Height, 3));

        // 上一行三格三等分，只剩一格的那一行整行铺满
        Assert.Equal(1d / 3, cells[0].Width, 3);
        Assert.Equal(1d / 3, cells[1].Width, 3);
        Assert.Equal(1d / 3, cells[2].Width, 3);
        Assert.Equal(1d, cells[3].Width, 3);
    }

    [Fact]
    public void 多于格数的页塞进最后一格()
    {
        var area = Area(6);
        area.ArrangeGrid(Ids(6), 2, 2);

        var cells = Cells(area.Root);
        Assert.Equal(4, cells.Count);
        Assert.Equal(["d3", "d4", "d5"], cells[3].Group.Tabs.Select(t => t.Id));
    }

    [Fact]
    public void 没进宫格的页留在第一格()
    {
        var area = Area(3);
        area.Open(new EditorTab("still", "静态图"));

        area.ArrangeGrid(["d0", "d1", "d2"], 1, 3);

        var cells = Cells(area.Root);
        Assert.Equal(3, cells.Count);
        Assert.Equal(["d0", "still"], cells[0].Group.Tabs.Select(t => t.Id));
    }

    [Fact]
    public void 排完宫格焦点回到第一格()
    {
        var area = Area(4);
        area.ArrangeGrid(Ids(4), 2, 2);

        Assert.Equal("d0", area.CurrentGroup.SelectedId);
        Assert.Same(Cells(area.Root)[0].Group, area.CurrentGroup);
    }

    [Fact]
    public void 收回单栏时页序不乱()
    {
        var area = Area(4);
        area.ArrangeGrid(Ids(4), 2, 2);

        // 焦点故意停在右下角那一格 —— 幸存组不该跟着焦点走
        area.Select("d3");
        area.CollapseToSingleGroup();

        var group = Assert.Single(area.Groups);
        Assert.Equal(["d0", "d1", "d2", "d3"], group.Tabs.Select(t => t.Id));
        Assert.Same(group, area.CurrentGroup);
    }

    [Fact]
    public void 反复换预设不残留()
    {
        var area = Area(6);
        var ids = Ids(6);

        foreach (var (rows, cols, cells) in new[] { (0, 0, 6), (3, 3, 6), (1, 2, 2), (2, 2, 4), (0, 0, 6) })
        {
            area.ArrangeGrid(ids, rows, cols);
            Assert.Equal(cells, area.Groups.Count);
            Assert.Equal(6, area.Groups.SelectMany(g => g.Tabs).Select(t => t.Id).Distinct().Count());
        }

        area.CollapseToSingleGroup();
        Assert.Equal(ids, Assert.Single(area.Groups).Tabs.Select(t => t.Id));
    }

    [Fact]
    public void 不足两页排不成宫格就收回单组()
    {
        var area = Area(3);
        area.ArrangeGrid(Ids(3), 1, 3);

        Assert.False(area.ArrangeGrid(["d0"], 2, 2));
        Assert.Single(area.Groups);
    }

    /// <summary>
    /// 选中「另一组里本来就选中着」的那一页，当前组必须跟过去。
    /// </summary>
    /// <remarks>
    /// 回归：<see cref="EditorArea.Select"/> 曾拿 <see cref="EditorGroup.Select"/> 的返回值
    /// 当「找到没有」用，而它在本来就选中时返回 false —— 于是整个方法空转，
    /// 焦点不过去、也不广播。
    /// </remarks>
    [Fact]
    public void 选中另一格里已选中的页焦点也要跟过去()
    {
        var area = Area(2);
        area.ArrangeGrid(Ids(2), 1, 2);
        area.Select("d1");
        Assert.Equal("d1", area.CurrentGroup.SelectedId);

        var changed = 0;
        area.Changed += (_, _) => changed++;

        Assert.True(area.Select("d0"));
        Assert.Equal("d0", area.CurrentGroup.SelectedId);
        Assert.Equal(1, changed);
    }

    [Theory]
    // 2 台：三个预设排出来一模一样，只有 1×2 该亮
    [InlineData(2, true, false, false)]
    // 3 台：1×2=一行两格（第三路叠在第二格）、2×2=2+1、3×3=一行三格，各不相同
    [InlineData(3, true, true, true)]
    [InlineData(4, true, true, true)]
    [InlineData(1, false, false, false)]
    public void 塌陷掉的预设不该可点(int cameras, bool g1x2, bool g2x2, bool g3x3)
    {
        Assert.Equal(g1x2, EditorGridPolicy.IsDistinct(cameras, 1, 2));
        Assert.Equal(g2x2, EditorGridPolicy.IsDistinct(cameras, 2, 2, (1, 2)));
        Assert.Equal(g3x3, EditorGridPolicy.IsDistinct(cameras, 3, 3, (1, 2), (2, 2)));
    }

    [Theory]
    [InlineData(2, 1, 2, 2)]
    [InlineData(4, 2, 2, 4)]
    [InlineData(9, 3, 3, 9)]
    [InlineData(3, 2, 2, 3)]
    public void 自动形状按台数(int cameras, int rows, int cols, int cells)
    {
        Assert.Equal((rows, cols, cells), EditorGridPolicy.Resolve(cameras, 0, 0));
    }
}
