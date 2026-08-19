using System.Collections.Generic;

namespace Piccolo.Forge.Model;

/// <summary>
/// 一个工具窗口的持久化状态。
/// </summary>
/// <remarks>
/// 逐字段对应上游
/// <c>platform/platform-impl/src/com/intellij/openapi/wm/impl/WindowInfoImpl.kt</c>，
/// 默认值与 XML 属性名见 <c>docs/upstream/工具窗口模型.md</c> §2。
/// <para>
/// 上游把「布局持有的可变 <c>WindowInfoImpl</c>」与「entry 持有的只读快照」分开
/// （<c>ToolWindowManagerImpl.kt:933-937</c>）。这里用不可变 record 表达快照，
/// 变更一律走 <c>with</c> 表达式。
/// </para>
/// </remarks>
public sealed record WindowInfo
{
    /// <summary>上游的默认权重常量 0.33f（<c>WindowInfoImpl.kt:35 · DEFAULT_WEIGHT</c>）。</summary>
    public const float DefaultWeight = 0.33f;

    /// <summary>默认的 pane id（<c>WindowInfo.kt:7 · WINDOW_INFO_DEFAULT_TOOL_WINDOW_PANE_ID</c>）。</summary>
    public const string DefaultPaneId = "root";

    private readonly float _weight = DefaultWeight;
    private readonly float _sideWeight = 0.5f;

    /// <summary>工具窗口 id（<c>WindowInfoImpl.kt:64</c>）。</summary>
    public required string Id { get; init; }

    /// <summary>所属 pane（<c>WindowInfoImpl.kt:41</c>，XML <c>toolWindowPaneId</c>）。</summary>
    public string PaneId { get; init; } = DefaultPaneId;

    /// <summary>停靠边（<c>WindowInfoImpl.kt:43-44</c>，XML <c>anchor</c>）。</summary>
    public ToolWindowAnchor Anchor { get; init; } = ToolWindowAnchor.Left;

    /// <summary>是否停在同一条边的「另一半」（<c>WindowInfoImpl.kt:91-92</c>，XML <c>side_tool</c>）。</summary>
    public bool IsSplit { get; init; }

    /// <summary>形态（<c>WindowInfoImpl.kt:74</c>，XML <c>type</c>）。</summary>
    public ToolWindowType Type { get; init; } = ToolWindowType.Docked;

    /// <summary>内部形态（<c>WindowInfoImpl.kt:71-72</c>，XML <c>internal_type</c>）。</summary>
    public ToolWindowType InternalType { get; init; } = ToolWindowType.Docked;

    /// <summary>内容区呈现方式（<c>WindowInfoImpl.kt:94-95</c>，XML <c>content_ui</c>）。</summary>
    public ToolWindowContentUiType ContentUiType { get; init; } = ToolWindowContentUiType.Tabbed;

    /// <summary>是否可见（<c>WindowInfoImpl.kt:76-77</c>，XML <c>visible</c>）。</summary>
    public bool IsVisible { get; init; }

    /// <summary>启动时是否取焦点（<c>WindowInfoImpl.kt:38-39</c>，XML <c>active</c>）。</summary>
    public bool IsActiveOnStart { get; init; }

    /// <summary>是否自动隐藏（<c>WindowInfoImpl.kt:46-47</c>，XML <c>auto_hide</c>）。</summary>
    public bool IsAutoHide { get; init; }

    /// <summary>是否最大化，仅对 WINDOWED 有意义（<c>WindowInfoImpl.kt:58-59</c>，XML <c>maximized</c>）。</summary>
    public bool IsMaximized { get; init; }

    /// <summary>是否在条带上显示按钮（<c>WindowInfoImpl.kt:79-80</c>，XML <c>show_stripe_button</c>）。默认 true。</summary>
    public bool IsShowStripeButton { get; init; } = true;

    /// <summary>
    /// 占该边的比例，写入时夹到 [0,1]（<c>WindowInfoImpl.kt:87</c>，XML <c>weight</c>）。
    /// </summary>
    public float Weight
    {
        get => _weight;
        init => _weight = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// 同一格里两个窗口的分配比例，写入时夹到 [0,1]（<c>WindowInfoImpl.kt:89</c>，XML <c>sideWeight</c>）。
    /// </summary>
    public float SideWeight
    {
        get => _sideWeight;
        init => _sideWeight = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// 按钮在条带内的次序；<c>-1</c> 表示未定序（<c>WindowInfoImpl.kt:100</c>，XML <c>order</c>）。
    /// </summary>
    public int Order { get; init; } = -1;

    /// <summary>浮动时的位置与尺寸；<see langword="null"/> 表示未定义（<c>WindowInfoImpl.kt:52-53</c>）。</summary>
    public FloatingBounds? FloatingBounds { get; init; }

    /// <summary>
    /// 标题条上那排内容 tab 的**顺序**（内容 id，从左到右）；空 = 没有内容 tab / 没排过。
    /// </summary>
    /// <remarks>
    /// 内容本身（有哪几块、叫什么）是宿主在登记时给的，不进存档；这里只存**用户拖出来的顺序**。
    /// 恢复时按这份把已登记的内容重排，存档里有、当前没有的 id 直接跳过
    /// （宿主改过内容集合，比如换版本删了一块）。
    /// <para>⚠ 与其它字段不同，这一支不是 <c>WindowInfo</c> 自己变出来的：
    /// 它由外壳在存盘前从各面板的 <c>Contents</c> 抓一次（见 <c>ToolWindowShellViewModel.SaveLayout</c>）。</para>
    /// </remarks>
    public IReadOnlyList<string> ContentOrder { get; init; } = [];

    /// <summary>是否来自持久化设置，不参与持久化（<c>WindowInfoImpl.kt:102-104</c>，<c>@Transient</c>）。</summary>
    public bool IsFromPersistentSettings { get; init; } = true;

    /// <summary>是否停靠形态（<c>WindowInfoImpl.kt:113-114 · isDocked</c>）。</summary>
    public bool IsDocked => Type == ToolWindowType.Docked;

    /// <summary>这个窗口占用的格子（见 <see cref="ToolWindowSlot"/>）。</summary>
    public ToolWindowSlot Slot => new(PaneId, Anchor, IsSplit, Type);
}

/// <summary>
/// 浮动窗口的位置与尺寸。
/// </summary>
/// <remarks>
/// 对应上游的 <c>java.awt.Rectangle</c>；持久化时扁平展开成四个属性
/// （<c>WindowInfoImpl.kt:52-53</c>），DTO 里则是 <c>List&lt;Int&gt;</c>
/// （<c>ToolWindowDefaultLayoutManager.kt:242</c>）。
/// </remarks>
/// <param name="X">左上角横坐标。</param>
/// <param name="Y">左上角纵坐标。</param>
/// <param name="Width">宽。</param>
/// <param name="Height">高。</param>
public readonly record struct FloatingBounds(int X, int Y, int Width, int Height);

/// <summary>
/// 「一个格子」的键：pane + 停靠边 + 是否另一半 + 形态。
/// </summary>
/// <remarks>
/// 出处 <c>ToolWindowManagerImpl.kt:962-966 · doShowWindow</c>：
/// 显示一个窗口时，这四项全相同的其它可见窗口会被隐藏。
/// 也就是说**一格只允许一个可见窗口**，而「格」不是「边」。
/// </remarks>
/// <param name="PaneId">所属 pane。</param>
/// <param name="Anchor">停靠边。</param>
/// <param name="IsSplit">是否是同一条边的另一半。</param>
/// <param name="Type">形态。</param>
public readonly record struct ToolWindowSlot(
    string PaneId,
    ToolWindowAnchor Anchor,
    bool IsSplit,
    ToolWindowType Type);
