using CommunityToolkit.Mvvm.ComponentModel;
using Piccolo.Forge.Model;

namespace Piccolo.Forge.Mvvm.CommunityToolkit;

/// <summary>
/// 面板的公共实现，基于 CommunityToolkit 的 <see cref="ObservableObject"/>。
/// </summary>
/// <remarks>
/// 与 <c>Piccolo.Forge.Mvvm.ForgePanelBase</c> 是同一件东西的两种绑定，二选一。
/// <b>选这个的理由只有一个</b>：宿主要在派生类里用 <c>[ObservableProperty]</c> /
/// <c>[RelayCommand]</c>，那些源生成器要求类继承 CommunityToolkit 的
/// <see cref="ObservableObject"/>。
/// </remarks>
public abstract class ForgePanelBase : ObservableObject, IForgePanel
{
    private string _title;
    private object? _icon;

    /// <summary>建一个面板。</summary>
    /// <param name="id">面板 id，建好之后不变。</param>
    protected ForgePanelBase(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        Id = id;
        _title = id;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    /// <remarks>可改：切语言时改这里，条带提示与标题条会一起跟着变。</remarks>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <inheritdoc />
    public object? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }
}

/// <summary>
/// 工具面板：**继承它，这个 VM 就是一块停在边上的工具窗口**。
/// </summary>
/// <remarks>停哪条边登记时说一次；按钮画在哪根条带是算出来的，宿主不用管。</remarks>
public abstract class ToolWindowPanel : ForgePanelBase, IToolWindowPanel
{
    /// <summary>建一块工具面板。</summary>
    /// <param name="id">面板 id。</param>
    protected ToolWindowPanel(string id) : base(id)
    {
    }
}

/// <summary>
/// 编辑区面板：**继承它，这个 VM 就活在中间那块**。
/// </summary>
public abstract class EditorPanel : ForgePanelBase, IEditorPanel
{
    /// <summary>建一块编辑区面板。</summary>
    /// <param name="id">面板 id。</param>
    protected EditorPanel(string id) : base(id)
    {
    }
}
