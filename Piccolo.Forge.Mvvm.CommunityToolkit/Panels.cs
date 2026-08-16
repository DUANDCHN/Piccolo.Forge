using CommunityToolkit.Mvvm.ComponentModel;
using Piccolo.Forge.Model;

namespace Piccolo.Forge.Mvvm.CommunityToolkit.Controls;

/// <summary>
/// 面板的公共实现，基于 CommunityToolkit 的 <see cref="ObservableObject"/>。
/// </summary>
/// <remarks>
/// 与 <c>Piccolo.Forge.Mvvm.Controls.ForgePanelBase</c> 是同一件东西的两种绑定，二选一。
/// 命名空间带 <c>.Controls</c> 的理由见中立包那份注释。
/// <b>选这个的理由只有一个</b>：宿主要在派生类里用 <c>[ObservableProperty]</c> /
/// <c>[RelayCommand]</c>，那些源生成器要求类继承 CommunityToolkit 的
/// <see cref="ObservableObject"/>。
/// </remarks>
public abstract class ForgePanelBase : ObservableObject, IForgePanel
{
    private string _id = string.Empty;
    private string _title = string.Empty;
    private object? _icon;

    /// <summary>建一个面板，稍后再给 <see cref="Id"/>。</summary>
    protected ForgePanelBase()
    {
    }

    /// <summary>建一个面板并当场给定身份。</summary>
    /// <param name="id">面板 id。</param>
    protected ForgePanelBase(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        Id = id;
        Title = id;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 可写 —— 与 Dock 的 <c>IDockable.Id</c>（"Gets or **sets** dockable id"）一致：
    /// 宿主常常是先无参造好面板、再由装配处赋 id。**赋在面板上就够了**，
    /// 外壳登记时从面板读，不要另存一份。
    /// </remarks>
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

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
    /// <summary>建一块工具面板，稍后再给 id。</summary>
    protected ToolWindowPanel()
    {
    }

    /// <summary>建一块工具面板并当场给定身份。</summary>
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
    /// <summary>建一块编辑区面板，稍后再给 id。</summary>
    protected EditorPanel()
    {
    }

    /// <summary>建一块编辑区面板并当场给定身份。</summary>
    /// <param name="id">面板 id。</param>
    protected EditorPanel(string id) : base(id)
    {
    }
}
