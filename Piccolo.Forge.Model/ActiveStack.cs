namespace Piccolo.Forge.Model;

/// <summary>
/// 「焦点该回到谁身上」的栈。实际上是两个栈。
/// </summary>
/// <typeparam name="T">栈里放的东西（上游是 <c>ToolWindowEntry</c>），按**引用**比较。</typeparam>
/// <remarks>
/// 对应上游 <c>platform/platform-impl/src/com/intellij/openapi/wm/impl/ActiveStack.java</c>。
/// <list type="bullet">
/// <item>易失栈（<c>:20 · myStack</c>）：编辑器拿到焦点时整个清空
/// （<c>ToolWindowManagerLifecycle.kt:85-92</c>）。</item>
/// <item>持久栈（<c>:25 · myPersistentStack</c>）：不因编辑器获焦而清，用来回答
/// 「最后一个活跃过的工具窗口是谁」。</item>
/// </list>
/// 「持久」只是相对易失栈而言，**它不落盘**。真正落盘的是另一份
/// <c>recentToolWindows</c>（<c>ToolWindowManagerState.kt:49</c>），别混。
/// </remarks>
public sealed class ActiveStack<T>
    where T : class
{
    private readonly List<T> _stack = [];
    private readonly List<T> _persistentStack = [];

    /// <summary>持久栈的深度（<c>ActiveStack.java:70-72 · getPersistentSize</c>）。</summary>
    public int PersistentCount => _persistentStack.Count;

    /// <summary>易失栈，**栈顶在前**（<c>ActiveStack.java:44-54</c>）。</summary>
    public IReadOnlyList<T> Stack => TopFirst(_stack);

    /// <summary>持久栈，**栈顶在前**（<c>ActiveStack.java:56-62</c>）。</summary>
    public IReadOnlyList<T> PersistentStack => TopFirst(_persistentStack);

    /// <summary>
    /// 压栈：先从**两个**栈里摘掉自己，再压进两个栈。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ActiveStack.java:64-68 · push</c>。先摘后压意味着同一个元素在栈里最多出现一次，
    /// 重复激活等于提到栈顶。
    /// </remarks>
    /// <param name="item">入栈的元素。</param>
    public void Push(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Remove(item, removePersistentAlso: true);
        _stack.Add(item);
        _persistentStack.Add(item);
    }

    /// <summary>
    /// 出栈。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ActiveStack.java:89-94 · remove</c>。
    /// <paramref name="removePersistentAlso"/> 为 <see langword="false"/> 时**只动易失栈** ——
    /// 隐藏一个工具窗口走的就是这条（<c>ToolWindowManagerImpl.kt:703</c>），
    /// 它得留在持久栈里，等重新可见时还能被
    /// <c>focusToolWindowByDefault</c> 兜底选到（<c>:1921-1941</c>）。
    /// 只有注销才两个栈都清（<c>:1203</c>）。
    /// </remarks>
    /// <param name="item">要摘掉的元素。</param>
    /// <param name="removePersistentAlso">是否连持久栈一起摘。</param>
    public void Remove(T item, bool removePersistentAlso)
    {
        ArgumentNullException.ThrowIfNull(item);
        RemoveByReference(_stack, item);
        if (removePersistentAlso)
        {
            RemoveByReference(_persistentStack, item);
        }
    }

    /// <summary>
    /// 清空易失栈，**持久栈不动**。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ActiveStack.java:36-38 · clear</c>；
    /// 触发点是编辑器拿到焦点（<c>ToolWindowManagerLifecycle.kt:85-92</c>）。
    /// </remarks>
    public void Clear() => _stack.Clear();

    /// <summary>
    /// 看持久栈的第 <paramref name="index"/> 个，<c>0</c> 是栈顶。
    /// </summary>
    /// <remarks>对应 <c>ActiveStack.java:77-80 · peekPersistent</c>。</remarks>
    /// <param name="index">从栈顶数起的下标。</param>
    /// <returns>该位置的元素。</returns>
    public T PeekPersistent(int index) => _persistentStack[_persistentStack.Count - index - 1];

    private static IReadOnlyList<T> TopFirst(List<T> source)
    {
        var result = new T[source.Count];
        for (var i = 0; i < source.Count; i++)
        {
            result[i] = source[source.Count - i - 1];
        }

        return result;
    }

    // 上游的元素是普通 class，没重写 equals（ToolWindowEntry.kt:35-37），所以是按引用摘
    private static void RemoveByReference(List<T> source, T item)
    {
        for (var i = source.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(source[i], item))
            {
                source.RemoveAt(i);
            }
        }
    }
}
