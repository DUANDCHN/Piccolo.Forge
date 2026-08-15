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
public readonly record struct ToolWindowContent(string Id, string DisplayName);

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

    /// <summary>选中的那块变了。</summary>
    public event EventHandler? SelectionChanged;

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
