using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// SideStack.java 的语义。
public class SideStackTests
{
    [Fact]
    public void Stack_is_disabled_by_default_upstream()
    {
        // registry.properties:1020 —— ide.enable.toolwindow.stack=false
        Assert.False(ToolWindowStackOptions.IsStackEnabledByDefault);
    }

    [Fact]
    public void Push_rejects_non_docked_windows()
    {
        // SideStack.java:20
        var stack = new SideStack();
        Assert.Throws<ArgumentException>(
            () => stack.Push(new WindowInfo { Id = "Preview", Type = ToolWindowType.Sliding }));
    }

    [Fact]
    public void Push_rejects_auto_hide_windows()
    {
        // SideStack.java:21
        var stack = new SideStack();
        Assert.Throws<ArgumentException>(
            () => stack.Push(new WindowInfo { Id = "Project", IsAutoHide = true }));
    }

    [Fact]
    public void Pop_returns_the_topmost_entry_with_the_same_anchor()
    {
        // SideStack.java:25-33
        var stack = new SideStack();
        stack.Push(new WindowInfo { Id = "Project", Anchor = ToolWindowAnchor.Left });
        stack.Push(new WindowInfo { Id = "Terminal", Anchor = ToolWindowAnchor.Bottom });
        stack.Push(new WindowInfo { Id = "Commit", Anchor = ToolWindowAnchor.Left });

        Assert.Equal("Commit", stack.Pop(ToolWindowAnchor.Left).Id);
        Assert.Equal("Project", stack.Pop(ToolWindowAnchor.Left).Id);
        Assert.True(stack.IsEmpty(ToolWindowAnchor.Left));
        Assert.False(stack.IsEmpty(ToolWindowAnchor.Bottom));
    }

    [Fact]
    public void Pop_on_an_empty_anchor_throws()
    {
        // 上游是数组越界（SideStack.java:26 的循环条件是 true），这里换成说得清的异常
        Assert.Throws<InvalidOperationException>(() => new SideStack().Pop(ToolWindowAnchor.Right));
    }

    [Fact]
    public void IsEmpty_only_looks_at_the_anchor()
    {
        // SideStack.java:39-47 —— isSplit / pane / type 的过滤在调用点做
        var stack = new SideStack();
        stack.Push(new WindowInfo { Id = "Structure", Anchor = ToolWindowAnchor.Left, IsSplit = true });

        Assert.False(stack.IsEmpty(ToolWindowAnchor.Left));
    }

    [Fact]
    public void Remove_drops_every_entry_with_that_id()
    {
        // SideStack.java:52-54
        var stack = new SideStack();
        stack.Push(new WindowInfo { Id = "Project", Anchor = ToolWindowAnchor.Left });
        stack.Push(new WindowInfo { Id = "Project", Anchor = ToolWindowAnchor.Left });

        stack.Remove("Project");

        Assert.True(stack.IsEmpty(ToolWindowAnchor.Left));
    }
}
