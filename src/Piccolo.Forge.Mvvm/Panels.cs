using Piccolo.Forge.Model;

namespace Piccolo.Forge.Mvvm;

/// <summary>
/// 面板的公共实现：身份 + 可观察。
/// </summary>
/// <remarks>
/// 对应 Dock 的 <c>DockableBase</c>。它落在**中立包**里，基于
/// <see cref="ForgeObservableObject"/>，不引任何 MVVM 框架。
/// <para>
/// 宿主如果已经在用 CommunityToolkit（想要 <c>[ObservableProperty]</c>），
/// 别继承这个，改用 <c>Piccolo.Forge.Mvvm.CommunityToolkit</c> 里同名的那套 ——
/// 这就是 Dock 分成 <c>Dock.Model.Mvvm</c> / <c>Dock.Model.Avalonia</c> 的原因：
/// **接口在中立包，基类按框架分包**。
/// </para>
/// </remarks>
public abstract class ForgePanelBase : ForgeObservableObject, IForgePanel
{
    private string _title = string.Empty;
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
/// <remarks>
/// 对应 Dock 的 <c>Dock.Model.Mvvm.Controls.Tool</c>（那个类自己也是 0 个成员，
/// 全部能力来自基类 + 标记接口）。
/// </remarks>
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
/// <remarks>它不会被停到侧边 —— 类型上就不成立，见 <see cref="IEditorPanel"/>。</remarks>
public abstract class EditorPanel : ForgePanelBase, IEditorPanel
{
    /// <summary>建一块编辑区面板。</summary>
    /// <param name="id">面板 id。</param>
    protected EditorPanel(string id) : base(id)
    {
    }
}
