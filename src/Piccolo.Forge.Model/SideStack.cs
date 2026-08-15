namespace Piccolo.Forge.Model;

/// <summary>
/// 「被挤掉的那个，等会儿放回来」的栈。
/// </summary>
/// <remarks>
/// 对应上游 <c>platform/platform-impl/src/com/intellij/openapi/wm/impl/SideStack.java</c>。
/// <para>
/// **上游默认是关的**：所有调用点都包在 <c>isStackEnabled</c> 里
/// （<c>ToolWindowManagerSupport.kt:87-88</c>），它读 Registry
/// <c>ide.enable.toolwindow.stack</c>，该项默认 <c>false</c>
/// （<c>platform/util/resources/misc/registry.properties:1020</c>）。
/// 见 <see cref="ToolWindowStackOptions.IsStackEnabledByDefault"/>。
/// </para>
/// <para>
/// 栈里只装副本（上游 <c>:22</c> 存的是 <c>info.copy()</c>）；这里的
/// <see cref="WindowInfo"/> 本身不可变，等价。
/// </para>
/// </remarks>
public sealed class SideStack
{
    private readonly List<WindowInfo> _stack = [];

    /// <summary>
    /// 压栈。要求是停靠形态且非自动隐藏。
    /// </summary>
    /// <remarks>
    /// 对应 <c>SideStack.java:19-23 · push</c>，两条断言照抄
    /// （<c>LOG.assertTrue(info.isDocked())</c>、<c>LOG.assertTrue(!info.isAutoHide())</c>）。
    /// 调用点自己也再判一次（<c>ToolWindowManagerImpl.kt:976</c>）。
    /// </remarks>
    /// <param name="info">被挤掉的窗口状态。</param>
    /// <exception cref="ArgumentException">不是停靠形态，或者是自动隐藏的。</exception>
    public void Push(WindowInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (!info.IsDocked)
        {
            throw new ArgumentException("SideStack 只收 DOCKED 的窗口（SideStack.java:20）", nameof(info));
        }

        if (info.IsAutoHide)
        {
            throw new ArgumentException("SideStack 不收 autoHide 的窗口（SideStack.java:21）", nameof(info));
        }

        _stack.Add(info);
    }

    /// <summary>
    /// 从栈顶往下找第一个同 <paramref name="anchor"/> 的，摘出来返回。
    /// </summary>
    /// <remarks>
    /// 对应 <c>SideStack.java:25-33 · pop</c>。上游的循环条件写的是 <c>true</c>，没有下界检查，
    /// 栈里没有该 anchor 的元素时会数组越界；所以调用前必须先
    /// <see cref="IsEmpty"/>（上游两处都是这么用的，<c>ToolWindowManagerImpl.kt:872</c>、<c>:887</c>）。
    /// 这里把越界换成一个说得清的异常。
    /// </remarks>
    /// <param name="anchor">要找的停靠边。</param>
    /// <returns>弹出的窗口状态。</returns>
    /// <exception cref="InvalidOperationException">栈里没有该 anchor 的元素。</exception>
    public WindowInfo Pop(ToolWindowAnchor anchor)
    {
        for (var i = _stack.Count - 1; i >= 0; i--)
        {
            if (_stack[i].Anchor == anchor)
            {
                var info = _stack[i];
                _stack.RemoveAt(i);
                return info;
            }
        }

        throw new InvalidOperationException($"栈里没有 anchor={anchor} 的元素，Pop 前要先判 IsEmpty");
    }

    /// <summary>
    /// 栈里有没有该 <paramref name="anchor"/> 的元素。
    /// </summary>
    /// <remarks>
    /// 对应 <c>SideStack.java:39-47 · isEmpty</c>：**只看 anchor**，
    /// 不看 isSplit / pane / type。那些过滤在调用点做
    /// （<c>ToolWindowManagerImpl.kt:889-899</c>）。
    /// </remarks>
    /// <param name="anchor">停靠边。</param>
    /// <returns>没有则 <see langword="true"/>。</returns>
    public bool IsEmpty(ToolWindowAnchor anchor) => !_stack.Exists(x => x.Anchor == anchor);

    /// <summary>
    /// 按 id 删掉**所有**匹配项。
    /// </summary>
    /// <remarks>
    /// 对应 <c>SideStack.java:52-54 · remove</c>。
    /// 一个窗口重新显示时会从栈里删掉自己（<c>ToolWindowManagerImpl.kt:994-995</c>），
    /// 注销时也删（<c>:1204-1205</c>）。
    /// </remarks>
    /// <param name="id">工具窗口 id。</param>
    public void Remove(string id) => _stack.RemoveAll(x => string.Equals(x.Id, id, StringComparison.Ordinal));

    /// <summary>清空（<c>SideStack.java:56-58 · clear</c>）。</summary>
    public void Clear() => _stack.Clear();
}

/// <summary>
/// 侧栈开关。
/// </summary>
public static class ToolWindowStackOptions
{
    /// <summary>
    /// 侧栈默认关闭。
    /// </summary>
    /// <remarks>
    /// Registry <c>ide.enable.toolwindow.stack</c> 默认 <c>false</c>
    /// （<c>platform/util/resources/misc/registry.properties:1020</c>），
    /// 读法见 <c>ToolWindowManagerSupport.kt:87-88 · isStackEnabled</c>。
    /// 也就是说**上游的默认行为是：隐藏一个窗口，不会自动把之前被它挤掉的那个放回来**。
    /// 复刻时别默认打开，那会做出一个「不像 Rider」的行为。
    /// </remarks>
    public const bool IsStackEnabledByDefault = false;
}
