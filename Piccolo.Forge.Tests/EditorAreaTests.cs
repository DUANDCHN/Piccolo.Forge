using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 关掉一个标签页之后选谁 —— 最容易做错、最影响手感的一条。
public class EditorSelectionPolicyTests
{
    private static readonly EditorTab[] Three =
    [
        new("a", "A"), new("b", "B"), new("c", "C"),
    ];

    [Fact]
    public void Closing_a_tab_that_is_not_selected_keeps_the_selection()
    {
        // EditorWindow.kt:826-830
        Assert.Equal(2, EditorSelectionPolicy.ComputeIndexToSelect(Three, closingIndex: 0, selectedIndex: 2));
    }

    [Fact]
    public void The_default_is_the_left_neighbour_not_the_right_one()
    {
        // :853-854 —— 两个开关默认都是 false（UISettingsState.kt:159、:162）
        Assert.False(EditorSelectionPolicy.ActiveMruEditorOnCloseDefault);
        Assert.False(EditorSelectionPolicy.ActiveRightEditorOnCloseDefault);

        Assert.Equal(0, EditorSelectionPolicy.ComputeIndexToSelect(Three, closingIndex: 1, selectedIndex: 1));
    }

    [Fact]
    public void Closing_the_first_tab_leaves_nothing_to_the_left()
    {
        Assert.Equal(-1, EditorSelectionPolicy.ComputeIndexToSelect(Three, closingIndex: 0, selectedIndex: 0));
    }

    [Fact]
    public void The_right_neighbour_only_with_that_setting_on()
    {
        // :849-851
        Assert.Equal(2, EditorSelectionPolicy.ComputeIndexToSelect(
            Three, closingIndex: 1, selectedIndex: 1, activeRightEditorOnClose: true));
    }

    [Fact]
    public void The_mru_setting_walks_the_history_backwards()
    {
        // :833-848
        var index = EditorSelectionPolicy.ComputeIndexToSelect(
            Three, closingIndex: 2, selectedIndex: 2,
            history: ["a", "b"], activeMruEditorOnClose: true);

        Assert.Equal(1, index);   // 历史里最新的是 b
    }

    [Fact]
    public void The_previous_selection_wins_over_everything()
    {
        // :771-777 —— 被关那个记着的「上一次选中」只要还在就选它
        var id = EditorSelectionPolicy.ComputeTabToSelect(
            Three, closingIndex: 2, selectedIndex: 2, previousSelectionId: "a");

        Assert.Equal("a", id);
    }

    [Fact]
    public void A_stale_previous_selection_is_ignored()
    {
        var id = EditorSelectionPolicy.ComputeTabToSelect(
            Three, closingIndex: 2, selectedIndex: 2, previousSelectionId: "已经关掉了");

        Assert.Equal("b", id);   // 退回左邻居
    }
}

/// 超限淘汰谁。
public class EditorTabLimitPolicyTests
{
    [Fact]
    public void The_limit_is_thirty()
        => Assert.Equal(30, EditorTabLimitPolicy.DefaultTabLimit);

    [Fact]
    public void Preview_tabs_are_closed_first_even_under_the_limit()
    {
        // EditorWindow.kt:1071-1073 —— 不管超没超限，预览页一律先关
        var tabs = new EditorTab[] { new("a", "A"), new("p", "P", IsPreview: true) };

        var closing = EditorTabLimitPolicy.ComputeTabsToClose(tabs, tabLimit: 30);

        Assert.Equal(["p"], closing);
    }

    [Fact]
    public void Pinned_tabs_are_never_evicted()
    {
        // :1173-1178 —— fileCanBeClosed 要求 !isFilePinned
        var tabs = new EditorTab[]
        {
            new("p1", "P1", IsPinned: true), new("p2", "P2", IsPinned: true), new("x", "X"),
        };

        var closing = EditorTabLimitPolicy.ComputeTabsToClose(tabs, tabLimit: 1);

        Assert.Equal(["x"], closing);
    }

    [Fact]
    public void Non_closeable_tabs_do_not_count_towards_the_limit()
    {
        // :1076 减去 :1180-1182
        var tabs = new EditorTab[]
        {
            new("fixed", "Fixed", IsCloseable: false), new("a", "A"),
        };

        Assert.True(EditorTabLimitPolicy.IsUnderLimit(tabs, tabLimit: 1));
        Assert.Empty(EditorTabLimitPolicy.ComputeTabsToClose(tabs, tabLimit: 1));
    }

    [Fact]
    public void Tabs_outside_the_history_are_evicted_first()
    {
        // :1099-1104
        var tabs = new EditorTab[]
        {
            new("old", "Old"), new("fresh", "Fresh", IsInHistory: false), new("b", "B"),
        };

        var closing = EditorTabLimitPolicy.ComputeTabsToClose(tabs, tabLimit: 2);

        Assert.Equal(["fresh"], closing);
    }

    [Fact]
    public void The_kept_tab_survives()
    {
        var tabs = new EditorTab[] { new("a", "A"), new("b", "B") };

        var closing = EditorTabLimitPolicy.ComputeTabsToClose(tabs, tabLimit: 1, keepId: "a");

        Assert.Equal(["b"], closing);
    }

    [Fact]
    public void Nothing_closable_means_no_infinite_loop()
    {
        var tabs = new EditorTab[] { new("p", "P", IsPinned: true), new("q", "Q", IsPinned: true) };

        Assert.Empty(EditorTabLimitPolicy.ComputeTabsToClose(tabs, tabLimit: 1));
    }
}

/// 分屏树。
public class EditorAreaTests
{
    [Fact]
    public void A_new_area_has_exactly_one_group()
    {
        var area = new EditorArea();

        Assert.Single(area.Groups);
        Assert.Same(area.Groups[0], area.CurrentGroup);
        Assert.IsType<EditorGroup>(area.Root);
    }

    [Fact]
    public void An_empty_group_cannot_be_split()
    {
        // EditorWindow.kt:528-530
        var area = new EditorArea();

        Assert.Null(area.Split(area.CurrentGroup, isVertical: false));
    }

    [Fact]
    public void Splitting_moves_only_the_selected_tab()
    {
        // :570-571 的注释原话：open only selected file in the new splitter instead of opening all tabs
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));
        area.Open(new EditorTab("b", "B"));
        var left = area.CurrentGroup;

        var right = area.Split(left, isVertical: false);

        Assert.NotNull(right);
        Assert.Equal(["b"], right.Tabs.Select(x => x.Id));
        Assert.Equal(["a", "b"], left.Tabs.Select(x => x.Id));   // 原组不动
        Assert.Same(right, area.CurrentGroup);
    }

    [Fact]
    public void The_split_proportion_starts_at_half_and_is_clamped()
    {
        // :549 —— proportion 0.5，可拖 [0.1, 0.9]
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));
        area.Split(area.CurrentGroup, isVertical: true);

        var split = Assert.IsType<EditorSplit>(area.Root);
        Assert.Equal(EditorSplit.DefaultProportion, split.Proportion);
        Assert.True(split.IsVertical);

        Assert.Equal(EditorSplit.MinProportion, new EditorSplit(new EditorGroup(), new EditorGroup(), false, 0f).Proportion);
        Assert.Equal(EditorSplit.MaxProportion, new EditorSplit(new EditorGroup(), new EditorGroup(), false, 1f).Proportion);
    }

    [Fact]
    public void The_tree_is_binary_so_three_panes_are_nested_splits()
    {
        // EditorsSplitters.kt:1270-1279 —— Splitter 只有 first/second
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));
        var second = area.Split(area.CurrentGroup, isVertical: false)!;
        area.Split(second, isVertical: false);

        Assert.Equal(3, area.Groups.Count);
        var root = Assert.IsType<EditorSplit>(area.Root);
        Assert.IsType<EditorSplit>(root.Second);   // 三栏 = 分屏里再分屏
    }

    [Fact]
    public void Unsplit_pulls_the_siblings_tabs_in()
    {
        // :955-967
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));
        var left = area.CurrentGroup;
        area.Split(left, isVertical: false);
        area.Open(new EditorTab("b", "B"));   // 落在新组

        Assert.True(area.Unsplit(left));

        Assert.Single(area.Groups);
        Assert.Equal(["a", "b"], left.Tabs.Select(x => x.Id));
    }

    [Fact]
    public void Unsplit_still_respects_the_tab_limit()
    {
        // :955-967 —— 搬的时候超了就丢掉不搬
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));
        var left = area.CurrentGroup;
        var right = area.Split(left, isVertical: false)!;
        right.Add(new EditorTab("b", "B"));
        right.Add(new EditorTab("c", "C"));

        area.Unsplit(left, tabLimit: 2);

        Assert.Equal(2, left.Tabs.Count);
    }

    [Fact]
    public void Closing_the_last_tab_of_a_group_removes_the_group()
    {
        // :791-820 —— 兄弟顶替整个分屏节点
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));
        var left = area.CurrentGroup;
        area.Split(left, isVertical: false);   // 新组里只有 a 的副本

        area.Close("a");   // 两组里都有 a？不——新组带走的是同一个 id

        Assert.Single(area.Groups);
        Assert.IsType<EditorGroup>(area.Root);
    }

    [Fact]
    public void The_group_remembers_the_previous_selection()
    {
        // 关掉当前页时优先回到它（EditorWindow.kt:771-777）
        var group = new EditorGroup();
        group.Add(new EditorTab("a", "A"));
        group.Add(new EditorTab("b", "B"));
        group.Add(new EditorTab("c", "C"));

        group.Select("a");
        group.Select("c");
        Assert.Equal("a", group.PreviousSelectedId);

        group.Close("c");

        Assert.Equal("a", group.SelectedId);
    }

    [Fact]
    public void Closing_a_non_selected_tab_leaves_the_selection_alone()
    {
        var group = new EditorGroup();
        group.Add(new EditorTab("a", "A"));
        group.Add(new EditorTab("b", "B"));
        group.Select("b");

        group.Close("a");

        Assert.Equal("b", group.SelectedId);
    }

    [Fact]
    public void Emptying_a_group_clears_the_selection()
    {
        var group = new EditorGroup();
        group.Add(new EditorTab("a", "A"));

        group.Close("a");

        Assert.Null(group.SelectedId);
        Assert.Null(group.PreviousSelectedId);
    }

    // ── 最大化 / 归一化（MaximizeEditorInSplitAction.kt）────────────────

    [Fact]
    public void Maximizing_pushes_the_group_side_to_its_extreme()
    {
        // MaximizeEditorInSplitAction.kt:100-111：在第一块那侧推到上限、第二块那侧推到下限。
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));
        var second = area.Split(area.CurrentGroup, isVertical: false)!;
        var split = (EditorSplit)area.Root;

        Assert.True(area.MaximizeOrNormalize(split.First.Groups.First()));
        Assert.Equal(EditorSplit.MaxProportion, split.Proportion);

        Assert.True(area.MaximizeOrNormalize(second));
        Assert.Equal(EditorSplit.MinProportion, split.Proportion);
    }

    [Fact]
    public void Maximizing_again_normalizes_everything()
    {
        // :43-47 —— 沿途已全在极值就走 getSplittersToNormalize，全部设回 0.5。
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));
        var b = area.Split(area.CurrentGroup, isVertical: false)!;
        area.SetCurrentGroup(b);
        area.Split(b, isVertical: true);

        var outer = (EditorSplit)area.Root;
        var inner = (EditorSplit)outer.Second;

        // b 在外层的第二块、内层的第一块
        Assert.True(area.MaximizeOrNormalize(b));
        Assert.Equal(EditorSplit.MinProportion, outer.Proportion);
        Assert.Equal(EditorSplit.MaxProportion, inner.Proportion);

        // 再来一次 → 全部归位（注意是**全部**，不只是刚才那一路）
        Assert.False(area.MaximizeOrNormalize(b));
        Assert.Equal(EditorSplit.DefaultProportion, outer.Proportion);
        Assert.Equal(EditorSplit.DefaultProportion, inner.Proportion);
    }

    [Fact]
    public void Normalizing_resets_splits_outside_the_path_too()
    {
        // 不对称是照抄上游的：最大化只推沿途，归一化推全部。
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));
        var b = area.Split(area.CurrentGroup, isVertical: false)!;
        var outer = (EditorSplit)area.Root;
        var a = outer.First.Groups.First();
        area.Split(a, isVertical: true);          // 左边再切一刀，b 的路上没有它
        var offPath = (EditorSplit)((EditorSplit)area.Root).First;

        offPath.Proportion = 0.8f;
        Assert.True(area.MaximizeOrNormalize(b));   // 只动了 outer
        Assert.Equal(0.8f, offPath.Proportion);

        Assert.False(area.MaximizeOrNormalize(b));  // 归一化动全部
        Assert.Equal(EditorSplit.DefaultProportion, offPath.Proportion);
    }

    [Fact]
    public void Maximizing_without_any_split_changes_nothing_and_reports_normalized()
    {
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));

        Assert.False(area.MaximizeOrNormalize(area.CurrentGroup));
        Assert.IsType<EditorGroup>(area.Root);
    }
}
