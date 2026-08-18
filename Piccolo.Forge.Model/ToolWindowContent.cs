namespace Piccolo.Forge.Model;

/// <summary>
/// 工具窗口里的一块内容。
/// </summary>
/// <remarks>
/// 对应上游 <c>com.intellij.ui.content.Content</c>：一个工具窗口可以装多块，
/// 由 <c>contentUiType</c> 决定画成一排 tab 还是一个下拉（<c>工具窗口模型.md</c> §13）。
/// </remarks>
/// <param name="Id">这块内容的 id。</param>
/// <param name="DisplayName">tab 上显示的名字。</param>
/// <param name="IsCloseable">这块内容能不能关。</param>
/// <param name="IsPinned">是不是被固定住了。</param>
public readonly record struct ToolWindowContent(
    string Id,
    string DisplayName,
    bool IsCloseable = true,
    bool IsPinned = false);

/// <summary>
/// 点 tab 上那个图标的结果。
/// </summary>
public enum TabIconAction
{
    /// <summary>没有图标，什么也不做。</summary>
    None,

    /// <summary>取消固定（固定住的内容点图标是解固定，不是关闭）。</summary>
    Unpin,

    /// <summary>关掉这块。</summary>
    Close,

    /// <summary>关掉其它所有（按住 Alt 时）。</summary>
    CloseOthers,
}

/// <summary>
/// 一个工具窗口里那几块内容，以及选中的是哪块。
/// </summary>
/// <remarks>对应上游的 <c>ContentManager</c>，这里只做选中与增删。</remarks>
public sealed class ToolWindowContentManager
{
    private readonly List<ToolWindowContent> _contents = [];
    private int _selectedIndex = -1;

    /// <summary>这个工具窗口里的所有内容，按加入顺序。</summary>
    public IReadOnlyList<ToolWindowContent> Contents => _contents;

    /// <summary>选中的那块；一块都没有时是 <see langword="null"/>。</summary>
    public ToolWindowContent? SelectedContent
        => _selectedIndex >= 0 && _selectedIndex < _contents.Count ? _contents[_selectedIndex] : null;

    /// <summary>
    /// 是不是只有一块内容。
    /// </summary>
    /// <remarks>
    /// 影响选中 tab 的前景色：只有一块时不换色
    /// （<c>ContentTabLabel.java:199-203</c>）。
    /// </remarks>
    public bool IsSoleContent => _contents.Count <= 1;

    /// <summary>
    /// 这个工具窗口允不允许关掉里面的内容。
    /// </summary>
    /// <remarks>
    /// 关闭图标出现的条件是「内容可关 **且** 工具窗口允许关内容」
    /// （<c>ContentTabLabel.java:169-171 · canBeClosed</c>）。
    /// </remarks>
    public bool CanCloseContents { get; init; } = true;

    /// <summary>选中的那块变了。</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    /// 这块内容的 tab 上该不该出现关闭图标。
    /// </summary>
    /// <remarks>对应 <c>ContentTabLabel.java:169-171</c>、<c>:244-247</c>。</remarks>
    /// <param name="content">内容。</param>
    /// <returns>该出现则为 <see langword="true"/>。</returns>
    public bool CanBeClosed(ToolWindowContent content) => content.IsCloseable && CanCloseContents;

    /// <summary>
    /// 点 tab 上那个图标会发生什么。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ContentTabLabel.java:250-257 · CloseContentTabAction.runAction</c>：
    /// **固定住的先解固定，不关**；按住 Alt 是关掉其它所有 tab。
    /// </remarks>
    /// <param name="id">内容 id。</param>
    /// <param name="isAltPressed">是否按住了 Alt。</param>
    /// <returns>实际发生的动作。</returns>
    public TabIconAction ResolveIconAction(string id, bool isAltPressed = false)
    {
        var content = _contents.Find(x => string.Equals(x.Id, id, StringComparison.Ordinal));
        if (string.IsNullOrEmpty(content.Id) || !CanBeClosed(content))
        {
            return TabIconAction.None;
        }

        if (content.IsPinned)
        {
            return TabIconAction.Unpin;
        }

        return isAltPressed ? TabIconAction.CloseOthers : TabIconAction.Close;
    }

    /// <summary>
    /// 执行 <see cref="ResolveIconAction"/> 算出来的动作。
    /// </summary>
    /// <param name="id">内容 id。</param>
    /// <param name="isAltPressed">是否按住了 Alt。</param>
    /// <returns>实际执行的动作。</returns>
    public TabIconAction InvokeIconAction(string id, bool isAltPressed = false)
    {
        var action = ResolveIconAction(id, isAltPressed);

        switch (action)
        {
            case TabIconAction.Unpin:
                var index = _contents.FindIndex(x => string.Equals(x.Id, id, StringComparison.Ordinal));
                _contents[index] = _contents[index] with { IsPinned = false };
                break;

            case TabIconAction.Close:
                Remove(id);
                break;

            case TabIconAction.CloseOthers:
                foreach (var other in _contents.Where(x => !string.Equals(x.Id, id, StringComparison.Ordinal)).ToList())
                {
                    Remove(other.Id);
                }

                break;
        }

        return action;
    }

    /// <summary>加一块内容；第一块自动选中。</summary>
    /// <param name="content">要加的内容。</param>
    public void Add(ToolWindowContent content)
    {
        _contents.Add(content);
        if (_selectedIndex < 0)
        {
            _selectedIndex = 0;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 把一块内容拖到另一块的位置上（重排）。
    /// </summary>
    /// <remarks>
    /// 语义**照抄文档标签那条**（<c>EditorSplitTree.Reorder</c> / 上游
    /// <c>JBTabsImpl.kt:3331 · reallocate</c>）：
    /// <c>先取目标索引 → 移除源 → 插回那个索引</c>。
    /// <para>
    /// ⚠ <b>索引在移除之前取，这不是笔误，是语义。</b>源在目标左边时，移除源让目标左移一格，
    /// 插回旧索引 = 落到目标**右边**；源在右边时插回旧索引 = 落到目标**左边**。
    /// 两个方向都是"越过目标"，这才是拖拽该有的手感；改成移除后再取索引，往右拖会永远差一格。
    /// </para>
    /// <para>
    /// **选中跟着内容走**：这里挪的是次序，不改"选中的是哪块"——
    /// 拖一下就换了选中会让人以为拖丢了。
    /// </para>
    /// </remarks>
    /// <param name="sourceId">被拖的那一块。</param>
    /// <param name="targetId">拖到谁的位置上。</param>
    /// <returns>确实挪动了则为 <see langword="true"/>。</returns>
    public bool Reorder(string sourceId, string targetId)
    {
        if (string.Equals(sourceId, targetId, StringComparison.Ordinal))
        {
            return false;
        }

        var targetIndex = _contents.FindIndex(x => string.Equals(x.Id, targetId, StringComparison.Ordinal));
        var sourceIndex = _contents.FindIndex(x => string.Equals(x.Id, sourceId, StringComparison.Ordinal));
        if (targetIndex < 0 || sourceIndex < 0)
        {
            return false;
        }

        var selectedId = SelectedContent?.Id;

        var source = _contents[sourceIndex];
        _contents.RemoveAt(sourceIndex);
        _contents.Insert(targetIndex, source);

        // 选中的是"哪一块"，不是"第几块" —— 重排后要把索引挪回那一块身上
        if (selectedId is not null)
        {
            _selectedIndex = _contents.FindIndex(x => string.Equals(x.Id, selectedId, StringComparison.Ordinal));
        }

        Reordered?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// 把一块内容挪到某条**插入缝**上。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Reorder"/> 的区别是"拖到谁身上" vs "拖到哪条缝里"：
    /// 界面上画的是缝（两块之间那条竖线），落位就该按缝算，两者才对得上。
    /// <para>
    /// <paramref name="insertionIndex"/> 是**当前列表**里的缝：0 = 最前面，
    /// <c>Count</c> = 最后面。源自己被移除会让它右边的缝各左移一格，这个调整**在这里做** ——
    /// 放到调用方去做，等于让每个界面各算一遍同一件事（算错就是"往右拖永远差一格"）。
    /// </para>
    /// </remarks>
    /// <param name="id">被拖的那一块。</param>
    /// <param name="insertionIndex">插到哪条缝里。</param>
    /// <returns>确实挪动了则为 <see langword="true"/>。</returns>
    public bool MoveTo(string id, int insertionIndex)
    {
        var sourceIndex = _contents.FindIndex(x => string.Equals(x.Id, id, StringComparison.Ordinal));
        if (sourceIndex < 0)
        {
            return false;
        }

        var target = Math.Clamp(insertionIndex, 0, _contents.Count);
        if (sourceIndex < target)
        {
            target--;   // 移除源之后，它右边的缝各左移一格
        }

        if (target == sourceIndex)
        {
            return false;
        }

        var selectedId = SelectedContent?.Id;

        var source = _contents[sourceIndex];
        _contents.RemoveAt(sourceIndex);
        _contents.Insert(target, source);

        if (selectedId is not null)
        {
            _selectedIndex = _contents.FindIndex(x => string.Equals(x.Id, selectedId, StringComparison.Ordinal));
        }

        Reordered?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>次序变了（重排）。</summary>
    /// <remarks>与 <see cref="SelectionChanged"/> 分开：重排不改选中，视图要做的事也不一样。</remarks>
    public event EventHandler? Reordered;

    /// <summary>
    /// 选中某块内容。
    /// </summary>
    /// <param name="id">内容 id。</param>
    /// <returns>找到并选中了则为 <see langword="true"/>。</returns>
    public bool Select(string id)
    {
        var index = _contents.FindIndex(x => string.Equals(x.Id, id, StringComparison.Ordinal));
        if (index < 0 || index == _selectedIndex)
        {
            return false;
        }

        _selectedIndex = index;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// 去掉一块内容。
    /// </summary>
    /// <remarks>去掉的正好是选中那块时，选中位置往前挪一格（末尾时挪到新的末尾）。</remarks>
    /// <param name="id">内容 id。</param>
    /// <returns>找到并去掉了则为 <see langword="true"/>。</returns>
    public bool Remove(string id)
    {
        var index = _contents.FindIndex(x => string.Equals(x.Id, id, StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }

        _contents.RemoveAt(index);

        if (_contents.Count == 0)
        {
            _selectedIndex = -1;
        }
        else if (index <= _selectedIndex)
        {
            _selectedIndex = Math.Max(0, Math.Min(_selectedIndex - 1, _contents.Count - 1));
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
