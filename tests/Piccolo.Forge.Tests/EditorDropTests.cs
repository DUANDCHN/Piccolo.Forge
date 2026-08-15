using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 拖一页到编辑区上的落点判定。
public class EditorDropPolicyTests
{
    private const double W = 100;
    private const double H = 100;

    [Fact]
    public void The_ratio_default_is_two_tenths()
        => Assert.Equal(0.2, EditorDropPolicy.DefaultDragToSplitRatio);

    [Theory]
    // 四条边各是个梯形（TabsUtil.java:59-85），往里收 r
    [InlineData(50, 5, EditorDropSide.Top)]
    [InlineData(5, 50, EditorDropSide.Left)]
    [InlineData(50, 95, EditorDropSide.Bottom)]
    [InlineData(95, 50, EditorDropSide.Right)]
    [InlineData(50, 50, EditorDropSide.Center)]
    public void Each_edge_band_maps_to_its_side(double x, double y, EditorDropSide expected)
        => Assert.Equal(expected, EditorDropPolicy.Resolve(x, y, W, H));

    [Fact]
    public void The_corner_is_split_by_the_diagonal_not_by_the_test_order()
    {
        // 四个梯形沿对角线相接、互不重叠（TabsUtil.java:59-85）：
        // 左上角这一片，靠上归「上」、靠左归「左」，分界就是那条对角线。
        Assert.Equal(EditorDropSide.Top, EditorDropPolicy.Resolve(10, 3, W, H));
        Assert.Equal(EditorDropSide.Left, EditorDropPolicy.Resolve(3, 10, W, H));

        // 右上角同理
        Assert.Equal(EditorDropSide.Top, EditorDropPolicy.Resolve(90, 3, W, H));
        Assert.Equal(EditorDropSide.Right, EditorDropPolicy.Resolve(97, 10, W, H));
    }

    [Fact]
    public void Outside_the_area_is_nothing()
    {
        Assert.Equal(EditorDropSide.None, EditorDropPolicy.Resolve(-5, 50, W, H));
        Assert.Equal(EditorDropSide.None, EditorDropPolicy.Resolve(50, 150, W, H));
    }

    [Fact]
    public void The_ratio_is_clamped()
    {
        // TabsUtil.java:55 —— max(.05, min(.45, …))
        Assert.Equal(EditorDropSide.Center, EditorDropPolicy.Resolve(50, 50, W, H, ratio: 10));
        Assert.Equal(EditorDropSide.Top, EditorDropPolicy.Resolve(50, 1, W, H, ratio: -1));
    }

    [Theory]
    [InlineData(EditorDropSide.Center, false)]
    [InlineData(EditorDropSide.None, false)]
    [InlineData(EditorDropSide.Top, true)]
    [InlineData(EditorDropSide.Right, true)]
    public void Only_the_edges_split(EditorDropSide side, bool expected)
        => Assert.Equal(expected, EditorDropPolicy.IsSplit(side));

    [Theory]
    [InlineData(EditorDropSide.Top, true, false)]
    [InlineData(EditorDropSide.Bottom, true, true)]
    [InlineData(EditorDropSide.Left, false, false)]
    [InlineData(EditorDropSide.Right, false, true)]
    public void The_side_decides_orientation_and_order(
        EditorDropSide side, bool vertical, bool newIsSecond)
    {
        Assert.Equal(vertical, EditorDropPolicy.IsVertical(side));
        Assert.Equal(newIsSecond, EditorDropPolicy.NewGroupIsSecond(side));
    }
}

/// 拖放的实际效果。
public class EditorDropTests
{
    [Fact]
    public void Dropping_on_an_edge_splits_and_moves_the_tab()
    {
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));
        area.Open(new EditorTab("b", "B"));
        var group = area.CurrentGroup;

        Assert.True(area.DropTab("b", group, EditorDropSide.Right));

        Assert.Equal(2, area.Groups.Count);
        var split = Assert.IsType<EditorSplit>(area.Root);
        Assert.False(split.IsVertical);                       // 左右切
        Assert.Equal(["a"], area.Groups[0].Tabs.Select(x => x.Id));
        Assert.Equal(["b"], area.Groups[1].Tabs.Select(x => x.Id));   // 新组在第二块
    }

    [Fact]
    public void Dropping_towards_the_top_puts_the_new_group_first()
    {
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));
        area.Open(new EditorTab("b", "B"));

        area.DropTab("b", area.CurrentGroup, EditorDropSide.Top);

        var split = Assert.IsType<EditorSplit>(area.Root);
        Assert.True(split.IsVertical);
        Assert.Equal(["b"], area.Groups[0].Tabs.Select(x => x.Id));   // 新组在前
    }

    [Fact]
    public void Dropping_in_the_centre_of_another_group_just_moves_it()
    {
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));
        area.Open(new EditorTab("b", "B"));
        var left = area.CurrentGroup;
        var right = area.Split(left, isVertical: false)!;

        Assert.True(area.DropTab("a", right, EditorDropSide.Center));

        Assert.Equal(2, area.Groups.Count);                    // 没有新分屏
        Assert.Contains(right.Tabs, x => x.Id == "a");
        Assert.DoesNotContain(left.Tabs, x => x.Id == "a");
    }

    [Fact]
    public void Dropping_in_its_own_centre_does_nothing()
    {
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));

        Assert.False(area.DropTab("a", area.CurrentGroup, EditorDropSide.Center));
        Assert.Single(area.Groups);
    }

    [Fact]
    public void Emptying_the_source_group_removes_it()
    {
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));
        var left = area.CurrentGroup;
        var right = area.Split(left, isVertical: false)!;
        right.Add(new EditorTab("b", "B"));

        // 把 right 里唯一「独有」的那页拖回 left 的中心
        area.DropTab("b", left, EditorDropSide.Center);

        Assert.Contains(left.Tabs, x => x.Id == "b");
    }
}

/// 预览标签页。
public class EditorPreviewTests
{
    [Fact]
    public void Pinning_a_preview_tab_promotes_it()
    {
        // EditorWindow.kt:1034-1040 —— 否则它一边被固定、一边还排在「第一批被关」里
        var group = new EditorGroup();
        group.Add(new EditorTab("a", "A", IsPreview: true));

        group.SetPinned("a", pinned: true);

        var tab = group.Tabs.Single();
        Assert.True(tab.IsPinned);
        Assert.False(tab.IsPreview);
    }

    [Fact]
    public void Unpinning_does_not_turn_it_back_into_a_preview()
    {
        var group = new EditorGroup();
        group.Add(new EditorTab("a", "A", IsPreview: true));
        group.SetPinned("a", pinned: true);

        group.SetPinned("a", pinned: false);

        Assert.False(group.Tabs.Single().IsPreview);
    }

    [Fact]
    public void A_preview_tab_can_be_promoted_explicitly()
    {
        // 宿主在「用户真的开始编辑它」时调这个
        var group = new EditorGroup();
        group.Add(new EditorTab("a", "A", IsPreview: true));

        Assert.True(group.PromoteFromPreview("a"));
        Assert.False(group.Tabs.Single().IsPreview);

        // 已经是正式页了，再调就没事可做
        Assert.False(group.PromoteFromPreview("a"));
    }

    [Fact]
    public void A_promoted_tab_is_no_longer_first_to_go()
    {
        var group = new EditorGroup();
        group.Add(new EditorTab("a", "A", IsPreview: true));
        group.Add(new EditorTab("b", "B"));

        group.PromoteFromPreview("a");

        Assert.Empty(EditorTabLimitPolicy.ComputeTabsToClose(group.Tabs, tabLimit: 30));
    }
}
