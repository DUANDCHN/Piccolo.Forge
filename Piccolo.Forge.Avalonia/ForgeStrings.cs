using System;
using Piccolo.Forge.Model;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 库里所有面向用户的文案（右键菜单、视图模式名、「移至」的位置名、拖动时的落点标签）。
/// </summary>
/// <remarks>
/// <para><b>库不自带语言。</b>默认 <see cref="English"/>；宿主按自己的语言设置换成
/// <see cref="ChineseSimplified"/> / <see cref="ChineseTraditional"/>，或者 <c>with</c> 出一份自己的。
/// 换语言后重建菜单即可生效（菜单每次弹出都是现造的）。</para>
/// <para>做成 <c>record</c> 而不是资源文件：AOT 友好、没有反射、宿主可以只改一两条。</para>
/// </remarks>
public sealed record ForgeStrings
{
    private static ForgeStrings? _current;

    /// <summary>当前生效的那套。宿主启动时设一次；没设就是 <see cref="English"/>。</summary>
    public static ForgeStrings Current
    {
        get => _current ?? English;
        set => _current = value;
    }

    // ── 工具窗口右键菜单 ──

    /// <summary>「移至 ▸」子菜单。</summary>
    public required string MoveTo { get; init; }

    /// <summary>「视图模式 ▸」子菜单。</summary>
    public required string ViewMode { get; init; }

    /// <summary>「从边栏中移除」（上游 RemoveStripeButtonAction）。</summary>
    public required string RemoveFromSidebar { get; init; }

    /// <summary>「显示工具窗口名称」开关。</summary>
    public required string ShowToolWindowNames { get; init; }

    // ── 视图模式名 ──

    /// <summary>视图模式「停靠固定」。</summary>
    public required string ViewModeDockPinned { get; init; }
    /// <summary>视图模式「停靠不固定」。</summary>
    public required string ViewModeDockUnpinned { get; init; }
    /// <summary>视图模式「取消停靠」。</summary>
    public required string ViewModeUndock { get; init; }
    /// <summary>视图模式「浮动」。</summary>
    public required string ViewModeFloat { get; init; }
    /// <summary>视图模式「窗口」。</summary>
    public required string ViewModeWindow { get; init; }

    // ── 位置：「哪条边 · 哪一半」 ──

    /// <summary>边：左。</summary>
    public required string SideLeft { get; init; }
    /// <summary>边：右。</summary>
    public required string SideRight { get; init; }
    /// <summary>边：底。</summary>
    public required string SideBottom { get; init; }
    /// <summary>边：顶。</summary>
    public required string SideTop { get; init; }
    /// <summary>半：上（竖边）。</summary>
    public required string HalfTop { get; init; }
    /// <summary>半：下（竖边）。</summary>
    public required string HalfBottom { get; init; }
    /// <summary>半：左（横边）。</summary>
    public required string HalfLeft { get; init; }
    /// <summary>半：右（横边）。</summary>
    public required string HalfRight { get; init; }

    /// <summary>位置名的拼法，{0} = 边、{1} = 半。</summary>
    public required string LocationFormat { get; init; }

    /// <summary>拖动时落点标签的拼法，{0} = 位置名。</summary>
    public required string MoveToFormat { get; init; }

    // ── 无障碍名（读屏器念的）：标题条内置按钮、「更多」按钮、关闭叉 ──

    /// <summary>标题条「取消停靠 / 停靠」按钮。</summary>
    public required string DockButton { get; init; }

    /// <summary>标题条「选项」（⋮）按钮。</summary>
    public required string OptionsButton { get; init; }

    /// <summary>标题条「隐藏」（—）按钮。</summary>
    public required string HideButton { get; init; }

    /// <summary>条带上的「更多工具窗口」按钮。</summary>
    public required string MoreToolWindows { get; init; }

    /// <summary>标签页上的关闭叉，{0} = 标签名。</summary>
    public required string CloseTabFormat { get; init; }

    // ── 编辑区标签页右键菜单 ──

    /// <summary>标签页「关闭」。</summary>
    public required string CloseTab { get; init; }
    /// <summary>「关闭其他标签页」。</summary>
    public required string CloseOtherTabs { get; init; }
    /// <summary>「关闭所有标签页」。</summary>
    public required string CloseAllTabs { get; init; }
    /// <summary>「关闭左侧标签页」。</summary>
    public required string CloseTabsToTheLeft { get; init; }
    /// <summary>「关闭右侧标签页」。</summary>
    public required string CloseTabsToTheRight { get; init; }
    /// <summary>「向右拆分」。</summary>
    public required string SplitRight { get; init; }
    /// <summary>「拆分并右移」。</summary>
    public required string SplitAndMoveRight { get; init; }
    /// <summary>「向下拆分」。</summary>
    public required string SplitDown { get; init; }
    /// <summary>「拆分并下移」。</summary>
    public required string SplitAndMoveDown { get; init; }
    /// <summary>「固定标签页」。</summary>
    public required string PinTab { get; init; }
    /// <summary>「在新窗口中打开标签页」。</summary>
    public required string OpenTabInNewWindow { get; init; }

    /// <summary>「哪条边 · 哪一半」。</summary>
    public string Describe(ToolWindowAnchor anchor, bool isSplit)
    {
        var side = anchor switch
        {
            ToolWindowAnchor.Left => SideLeft,
            ToolWindowAnchor.Right => SideRight,
            ToolWindowAnchor.Bottom => SideBottom,
            _ => SideTop,
        };

        // 竖边分上下，横边分左右（上游 getLocationIcon：竖边的另一半在下，横边的另一半在右）
        var half = anchor is ToolWindowAnchor.Bottom or ToolWindowAnchor.Top
            ? isSplit ? HalfRight : HalfLeft
            : isSplit ? HalfBottom : HalfTop;

        return string.Format(LocationFormat, side, half);
    }

    /// <summary>视图模式的名字。</summary>
    public string Describe(ToolWindowViewMode mode) => mode switch
    {
        ToolWindowViewMode.DockPinned => ViewModeDockPinned,
        ToolWindowViewMode.DockUnpinned => ViewModeDockUnpinned,
        ToolWindowViewMode.Undock => ViewModeUndock,
        ToolWindowViewMode.Float => ViewModeFloat,
        ToolWindowViewMode.Window => ViewModeWindow,
        _ => mode.ToString(),
    };

    /// <summary>英文（默认）。</summary>
    public static ForgeStrings English { get; } = new()
    {
        MoveTo = "Move to",
        ViewMode = "View Mode",
        RemoveFromSidebar = "Remove from Sidebar",
        ShowToolWindowNames = "Show Tool Window Names",
        ViewModeDockPinned = "Dock Pinned",
        ViewModeDockUnpinned = "Dock Unpinned",
        ViewModeUndock = "Undock",
        ViewModeFloat = "Float",
        ViewModeWindow = "Window",
        SideLeft = "Left",
        SideRight = "Right",
        SideBottom = "Bottom",
        SideTop = "Top",
        HalfTop = "Top",
        HalfBottom = "Bottom",
        HalfLeft = "Left",
        HalfRight = "Right",
        LocationFormat = "{0} · {1}",
        MoveToFormat = "Move to {0}",
        CloseTab = "Close",
        CloseOtherTabs = "Close Other Tabs",
        CloseAllTabs = "Close All Tabs",
        CloseTabsToTheLeft = "Close Tabs to the Left",
        CloseTabsToTheRight = "Close Tabs to the Right",
        SplitRight = "Split Right",
        SplitAndMoveRight = "Split and Move Right",
        SplitDown = "Split Down",
        SplitAndMoveDown = "Split and Move Down",
        PinTab = "Pin Tab",
        OpenTabInNewWindow = "Open in New Window",
        DockButton = "Dock",
        OptionsButton = "Options",
        HideButton = "Hide",
        MoreToolWindows = "More tool windows",
        CloseTabFormat = "Close {0}",
    };

    /// <summary>简体中文。</summary>
    public static ForgeStrings ChineseSimplified { get; } = new()
    {
        MoveTo = "移至",
        ViewMode = "视图模式",
        RemoveFromSidebar = "从边栏中移除",
        ShowToolWindowNames = "显示工具窗口名称",
        ViewModeDockPinned = "停靠固定",
        ViewModeDockUnpinned = "停靠不固定",
        ViewModeUndock = "取消停靠",
        ViewModeFloat = "浮动",
        ViewModeWindow = "窗口",
        SideLeft = "左侧",
        SideRight = "右侧",
        SideBottom = "底部",
        SideTop = "顶部",
        HalfTop = "上半",
        HalfBottom = "下半",
        HalfLeft = "左半",
        HalfRight = "右半",
        LocationFormat = "{0} · {1}",
        MoveToFormat = "移至 {0}",
        CloseTab = "关闭",
        CloseOtherTabs = "关闭其他标签页",
        CloseAllTabs = "关闭所有标签页",
        CloseTabsToTheLeft = "关闭左侧标签页",
        CloseTabsToTheRight = "关闭右侧标签页",
        SplitRight = "向右拆分",
        SplitAndMoveRight = "拆分并右移",
        SplitDown = "向下拆分",
        SplitAndMoveDown = "拆分并下移",
        PinTab = "固定标签页",
        OpenTabInNewWindow = "在新窗口中打开标签页",
        DockButton = "停靠",
        OptionsButton = "选项",
        HideButton = "隐藏",
        MoreToolWindows = "更多工具窗口",
        CloseTabFormat = "关闭 {0}",
    };

    /// <summary>繁体中文。</summary>
    public static ForgeStrings ChineseTraditional { get; } = ChineseSimplified with
    {
        MoveTo = "移至",
        ViewMode = "檢視模式",
        RemoveFromSidebar = "從側邊欄移除",
        ShowToolWindowNames = "顯示工具視窗名稱",
        ViewModeDockPinned = "停靠固定",
        ViewModeDockUnpinned = "停靠不固定",
        ViewModeUndock = "取消停靠",
        ViewModeFloat = "浮動",
        ViewModeWindow = "視窗",
        SideLeft = "左側",
        SideRight = "右側",
        SideBottom = "底部",
        SideTop = "頂部",
        CloseTab = "關閉",
        CloseOtherTabs = "關閉其他分頁",
        CloseAllTabs = "關閉所有分頁",
        CloseTabsToTheLeft = "關閉左側分頁",
        CloseTabsToTheRight = "關閉右側分頁",
        SplitRight = "向右分割",
        SplitAndMoveRight = "分割並右移",
        SplitDown = "向下分割",
        SplitAndMoveDown = "分割並下移",
        PinTab = "固定分頁",
        OpenTabInNewWindow = "在新視窗中開啟分頁",
        DockButton = "停靠",
        OptionsButton = "選項",
        HideButton = "隱藏",
        MoreToolWindows = "更多工具視窗",
        CloseTabFormat = "關閉 {0}",
    };
}
