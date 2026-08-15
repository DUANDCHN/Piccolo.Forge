using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// ActiveStack.java 的两个栈。
public class ActiveStackTests
{
    private sealed class Entry(string id)
    {
        public string Id { get; } = id;
        public override string ToString() => Id;
    }

    [Fact]
    public void Stack_is_top_first()
    {
        // ActiveStack.java:44-54 —— peek(i) = get(size - i - 1)
        var (stack, a, b, c) = PushThree();

        Assert.Equal([c, b, a], stack.Stack);
        Assert.Equal([c, b, a], stack.PersistentStack);
    }

    [Fact]
    public void Push_moves_an_existing_entry_to_the_top()
    {
        // ActiveStack.java:64-68 —— push 先 remove(id, true)
        var (stack, a, b, c) = PushThree();

        stack.Push(a);

        Assert.Equal([a, c, b], stack.Stack);
        Assert.Equal([a, c, b], stack.PersistentStack);
        Assert.Equal(3, stack.PersistentCount);
    }

    [Fact]
    public void Remove_without_persistent_keeps_it_in_the_persistent_stack()
    {
        // ActiveStack.java:89-94，隐藏走的就是这条（ToolWindowManagerImpl.kt:703）
        var (stack, a, b, c) = PushThree();

        stack.Remove(b, removePersistentAlso: false);

        Assert.Equal([c, a], stack.Stack);
        Assert.Equal([c, b, a], stack.PersistentStack);
    }

    [Fact]
    public void Remove_with_persistent_clears_both()
    {
        // 只有注销才两个栈都清（ToolWindowManagerImpl.kt:1203）
        var (stack, a, b, c) = PushThree();

        stack.Remove(b, removePersistentAlso: true);

        Assert.Equal([c, a], stack.Stack);
        Assert.Equal([c, a], stack.PersistentStack);
    }

    [Fact]
    public void Clear_only_clears_the_volatile_stack()
    {
        // ActiveStack.java:36-38，触发点是编辑器获焦（ToolWindowManagerLifecycle.kt:85-92）
        var (stack, a, b, c) = PushThree();

        stack.Clear();

        Assert.Empty(stack.Stack);
        Assert.Equal([c, b, a], stack.PersistentStack);
    }

    [Fact]
    public void PeekPersistent_counts_from_the_top()
    {
        // ActiveStack.java:77-80
        var (stack, a, _, c) = PushThree();

        Assert.Same(c, stack.PeekPersistent(0));
        Assert.Same(a, stack.PeekPersistent(2));
    }

    [Fact]
    public void Entries_are_compared_by_reference()
    {
        // ToolWindowEntry.kt:35-37 是普通 class，没重写 equals
        var stack = new ActiveStack<Entry>();
        var first = new Entry("Project");
        var second = new Entry("Project");

        stack.Push(first);
        stack.Push(second);

        Assert.Equal([second, first], stack.Stack);
    }

    private static (ActiveStack<Entry> Stack, Entry A, Entry B, Entry C) PushThree()
    {
        var stack = new ActiveStack<Entry>();
        var a = new Entry("a");
        var b = new Entry("b");
        var c = new Entry("c");
        stack.Push(a);
        stack.Push(b);
        stack.Push(c);
        return (stack, a, b, c);
    }
}
