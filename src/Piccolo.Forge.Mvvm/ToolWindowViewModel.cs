using Piccolo.Forge.Model;

namespace Piccolo.Forge.Mvvm;

/// <summary>
/// 一个工具窗口的可观察视图。
/// </summary>
/// <remarks>
/// 它是 <see cref="WindowInfo"/> 的**投影**，不是第二份真相：
/// 位置与可见性仍然由 <see cref="DesktopLayout"/> 说了算，这里只负责让 UI 能绑。
/// <para>
/// <see cref="Content"/> 是 <see cref="object"/> 而不是控件 ——
/// 宿主放 VM 进来，视图由宿主自己的 ViewLocator / DataTemplate 解析。
/// 控件库不认识宿主的视图。
/// </para>
/// </remarks>
public sealed class ToolWindowViewModel : ObservableObject
{
    private string _title;
    private object? _icon;
    private object? _content;
    private bool _isOpen;
    private bool _isActive;
    private bool _isShowStripeButton = true;
    private ToolWindowAnchor _anchor;
    private bool _isSplit;
    private int _order = -1;

    /// <summary>建一个工具窗口的 VM。</summary>
    /// <param name="id">工具窗口 id，建好之后不变。</param>
    /// <param name="title">显示名。</param>
    public ToolWindowViewModel(string id, string? title = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        Id = id;
        _title = title ?? id;
    }

    /// <summary>工具窗口 id。</summary>
    public string Id { get; }

    /// <summary>显示名：条带按钮的提示、标题条上的标题。</summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>
    /// 条带按钮上的图标。
    /// </summary>
    /// <remarks>
    /// 同样是 <see cref="object"/>：宿主可以塞自己的图标控件、几何图形或字符。
    /// Forge 不带图标资源（原因见 <c>工具窗口模型.md</c> §17）。
    /// </remarks>
    public object? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    /// <summary>面板里放什么。宿主的 VM，视图由宿主解析。</summary>
    public object? Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    /// <summary>现在是不是可见（对应 <see cref="WindowInfo.IsVisible"/>）。</summary>
    public bool IsOpen
    {
        get => _isOpen;
        internal set => SetProperty(ref _isOpen, value);
    }

    /// <summary>是不是当前活跃的那个（决定条带按钮与标题条的高亮）。</summary>
    public bool IsActive
    {
        get => _isActive;
        internal set => SetProperty(ref _isActive, value);
    }

    /// <summary>条带上要不要有它的按钮（对应 <see cref="WindowInfo.IsShowStripeButton"/>）。</summary>
    public bool IsShowStripeButton
    {
        get => _isShowStripeButton;
        internal set => SetProperty(ref _isShowStripeButton, value);
    }

    /// <summary>停靠边。</summary>
    public ToolWindowAnchor Anchor
    {
        get => _anchor;
        internal set
        {
            if (SetProperty(ref _anchor, value))
            {
                OnPropertyChanged(nameof(Placement));
            }
        }
    }

    /// <summary>是不是同一条边的另一半。</summary>
    public bool IsSplit
    {
        get => _isSplit;
        internal set
        {
            if (SetProperty(ref _isSplit, value))
            {
                OnPropertyChanged(nameof(Placement));
            }
        }
    }

    /// <summary>条带内的次序。</summary>
    public int Order
    {
        get => _order;
        internal set => SetProperty(ref _order, value);
    }

    /// <summary>
    /// 按钮该画在哪根条带的哪一组 —— 派生量，不持久化。
    /// </summary>
    /// <remarks>见 <see cref="StripePlacementResolver.Resolve"/>。</remarks>
    public StripePlacement Placement => StripePlacementResolver.Resolve(Anchor, IsSplit);

    /// <summary>这个工具窗口里的那几块内容。</summary>
    public ToolWindowContentManager Contents { get; } = new();

    /// <summary>从模型状态同步一次。</summary>
    /// <param name="info">模型里的状态。</param>
    /// <param name="isActive">是不是当前活跃的那个。</param>
    internal void SyncFrom(WindowInfo info, bool isActive)
    {
        ArgumentNullException.ThrowIfNull(info);

        IsOpen = info.IsVisible;
        IsShowStripeButton = info.IsShowStripeButton;
        Anchor = info.Anchor;
        IsSplit = info.IsSplit;
        Order = info.Order;
        IsActive = isActive;
    }
}
