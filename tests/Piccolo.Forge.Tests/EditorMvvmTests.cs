using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Mvvm.Controls;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 编辑区的可观察层。
public class EditorMvvmTests
{
    private sealed class ImageDoc(string id) : EditorPanel(id);

    [Fact]
    public void Opening_a_panel_takes_its_identity_from_the_panel()
    {
        var editor = new EditorAreaViewModel();
        var doc = new ImageDoc("cam-1") { Title = "相机 1", Icon = "C" };

        var tab = editor.Open(doc);

        Assert.Equal("cam-1", tab.Id);
        Assert.Equal("相机 1", tab.Title);
        Assert.Equal("C", tab.Icon);
        Assert.Same(doc, tab.Content);
    }

    [Fact]
    public void The_first_tab_is_selected()
    {
        var editor = new EditorAreaViewModel();
        var tab = editor.Open("a", "A");

        Assert.True(tab.IsSelected);
        Assert.Same(tab, editor.Groups[0].Selected);
    }

    [Fact]
    public void Selecting_flips_IsSelected_on_both()
    {
        var editor = new EditorAreaViewModel();
        var a = editor.Open("a", "A");
        var b = editor.Open("b", "B");

        editor.Select("a");

        Assert.True(a.IsSelected);
        Assert.False(b.IsSelected);
    }

    [Fact]
    public void Closing_a_tab_does_not_rebuild_the_others()
    {
        // 与工具窗口那边同一条规矩：宿主塞的内容不能被重造
        var editor = new EditorAreaViewModel();
        var a = editor.Open("a", "A");
        var live = new object();
        a.Content = live;
        editor.Open("b", "B");

        editor.Close("b");

        Assert.Same(a, editor.Find("a"));
        Assert.Same(live, a.Content);
    }

    [Fact]
    public void Splitting_shows_up_as_two_groups()
    {
        var editor = new EditorAreaViewModel();
        editor.Open("a", "A");
        editor.Open("b", "B");

        editor.Area.Split(editor.Area.CurrentGroup, isVertical: false);

        Assert.Equal(2, editor.Groups.Count);
        Assert.True(editor.Groups[1].IsCurrent);
        Assert.False(editor.Groups[0].IsCurrent);
    }

    [Fact]
    public void The_same_tab_vm_is_reused_across_groups()
    {
        // 分屏把选中那个带过去；VM 是同一个，内容不重造
        var editor = new EditorAreaViewModel();
        var a = editor.Open("a", "A");
        a.Content = new object();
        var content = a.Content;

        editor.Area.Split(editor.Area.CurrentGroup, isVertical: true);

        Assert.Same(a, editor.Find("a"));
        Assert.Same(content, a.Content);
    }

    [Fact]
    public void Unsplitting_collapses_the_groups_back()
    {
        var editor = new EditorAreaViewModel();
        editor.Open("a", "A");
        var left = editor.Area.CurrentGroup;
        editor.Area.Split(left, isVertical: false);
        Assert.Equal(2, editor.Groups.Count);

        editor.Area.Unsplit(left);

        Assert.Single(editor.Groups);
    }

    [Fact]
    public void Pin_state_reaches_the_view_model()
    {
        var editor = new EditorAreaViewModel();
        var tab = editor.Open("a", "A");
        Assert.False(tab.IsPinned);

        // 走编辑区这一层，才有变更通知
        Assert.True(editor.Area.SetPinned("a", pinned: true));

        Assert.True(tab.IsPinned);
    }

    [Fact]
    public void Restoring_puts_the_tree_back_and_lets_the_host_reclaim_content()
    {
        var saved = new EditorAreaViewModel();
        saved.Open(new ImageDoc("cam-1") { Title = "相机 1" });
        saved.Open(new ImageDoc("cam-2") { Title = "相机 2" });
        saved.Area.SetPinned("cam-1", true);
        saved.Area.Split(saved.Area.CurrentGroup, isVertical: false);
        var xml = EditorLayoutSerializer.Write(saved.Area);

        var restored = new EditorAreaViewModel();
        var dropped = restored.Restore(xml, id => new ImageDoc(id) { Title = id });

        Assert.Empty(dropped);
        Assert.Equal(2, restored.Groups.Count);
        var tab = restored.Find("cam-1");
        Assert.NotNull(tab);
        Assert.True(tab!.IsPinned);
        Assert.IsType<ImageDoc>(tab.Content);
    }

    [Fact]
    public void A_tab_the_host_no_longer_recognises_is_dropped()
    {
        // 上游 EditorsSplitters.kt:1748-1768 resolveFileOrLogError：
        // 解析不到文件就记 warn 然后返回 null，那一页不再恢复。
        var saved = new EditorAreaViewModel();
        saved.Open(new ImageDoc("still-here"));
        saved.Open(new ImageDoc("deleted"));
        var xml = EditorLayoutSerializer.Write(saved.Area);

        var restored = new EditorAreaViewModel();
        var dropped = restored.Restore(xml, id => id == "deleted" ? null : new ImageDoc(id));

        Assert.Equal(["deleted"], dropped);
        Assert.Null(restored.Find("deleted"));
        Assert.NotNull(restored.Find("still-here"));
        Assert.Single(restored.Groups[0].Tabs);
    }

    [Fact]
    public void Restoring_without_a_factory_keeps_every_tab_with_empty_content()
    {
        var saved = new EditorAreaViewModel();
        saved.Open(new ImageDoc("cam-1"));
        var xml = EditorLayoutSerializer.Write(saved.Area);

        var restored = new EditorAreaViewModel();
        Assert.Empty(restored.Restore(xml));

        Assert.Single(restored.Groups[0].Tabs);
        Assert.Null(restored.Groups[0].Tabs[0].Content);
    }
}
