using System;
using Piccolo.Forge.Model;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 库里所有面向用户的文案（右键菜单、视图模式名、「移至」的位置名、拖动时的落点标签、无障碍名）。
/// </summary>
/// <remarks>
/// <para><b>真值在资源字典里</b>（<c>Locales/*.axaml</c>，键 <c>Forge.Strings.*</c>），与颜色键同一套机制：
/// 宿主用 <see cref="ForgeThemeManager.ApplyLocale(global::Avalonia.Application, System.Globalization.CultureInfo?)"/> 按 <c>CultureInfo</c> 挑一份装进
/// <c>Application.Resources</c>（照 Semi 的 <c>SemiTheme.Locale</c>），想改哪句就重定义同名键。
/// 模板里的文字走 <c>{DynamicResource}</c> 可即时刷新；C# 造的菜单每次弹出现取。</para>
/// <para>本类是**类型化门面**：<see cref="Current"/> 按 <c>Application.Current.Resources</c> 现读一份；
/// 没装任何语言（没跑 Avalonia、或宿主没调 ApplyLocale）时回落到 <see cref="English"/>。
/// <see cref="Override"/> 给不走 XAML 的宿主与测试：设了就以它为准。</para>
/// </remarks>
public sealed record ForgeStrings
{
    /// <summary>不走资源字典、直接整套给的文案；设了就压过资源。测试与纯代码宿主用。</summary>
    public static ForgeStrings? Override { get; set; }

    private static ForgeStrings? _cached;
    private static object? _cachedFrom;

    /// <summary>当前生效的那套：<see cref="Override"/> → 应用资源里的 <c>Forge.Strings.*</c> → <see cref="English"/>。</summary>
    public static ForgeStrings Current
    {
        get
        {
            if (Override is { } o) return o;
            var app = global::Avalonia.Application.Current;
            if (app is null || !app.TryGetResource("Forge.Strings.MoveTo", null, out _)) return English;

            // 资源字典换过就重读（同一份字典时只读一次）
            var stamp = ForgeThemeManager.InstalledLocale;
            if (_cached is not null && ReferenceEquals(_cachedFrom, stamp)) return _cached;
            _cached = FromResources(app);
            _cachedFrom = stamp;
            return _cached;
        }
    }

    private static ForgeStrings FromResources(global::Avalonia.Application app)
    {
        string R(string key, string fallback)
            => app.TryGetResource("Forge.Strings." + key, null, out var v) && v is string text ? text : fallback;

        var e = English;
        return new ForgeStrings
        {
            MoveTo = R(nameof(MoveTo), e.MoveTo), ViewMode = R(nameof(ViewMode), e.ViewMode),
            RemoveFromSidebar = R(nameof(RemoveFromSidebar), e.RemoveFromSidebar), ShowToolWindowNames = R(nameof(ShowToolWindowNames), e.ShowToolWindowNames),
            ViewModeDockPinned = R(nameof(ViewModeDockPinned), e.ViewModeDockPinned), ViewModeDockUnpinned = R(nameof(ViewModeDockUnpinned), e.ViewModeDockUnpinned),
            ViewModeUndock = R(nameof(ViewModeUndock), e.ViewModeUndock), ViewModeFloat = R(nameof(ViewModeFloat), e.ViewModeFloat), ViewModeWindow = R(nameof(ViewModeWindow), e.ViewModeWindow),
            SideLeft = R(nameof(SideLeft), e.SideLeft), SideRight = R(nameof(SideRight), e.SideRight), SideBottom = R(nameof(SideBottom), e.SideBottom), SideTop = R(nameof(SideTop), e.SideTop),
            HalfTop = R(nameof(HalfTop), e.HalfTop), HalfBottom = R(nameof(HalfBottom), e.HalfBottom), HalfLeft = R(nameof(HalfLeft), e.HalfLeft), HalfRight = R(nameof(HalfRight), e.HalfRight),
            LocationFormat = R(nameof(LocationFormat), e.LocationFormat), MoveToFormat = R(nameof(MoveToFormat), e.MoveToFormat),
            DockButton = R(nameof(DockButton), e.DockButton), OptionsButton = R(nameof(OptionsButton), e.OptionsButton), HideButton = R(nameof(HideButton), e.HideButton),
            MoreToolWindows = R(nameof(MoreToolWindows), e.MoreToolWindows), CloseTabFormat = R(nameof(CloseTabFormat), e.CloseTabFormat),
            CloseTab = R(nameof(CloseTab), e.CloseTab), CloseOtherTabs = R(nameof(CloseOtherTabs), e.CloseOtherTabs), CloseAllTabs = R(nameof(CloseAllTabs), e.CloseAllTabs),
            CloseTabsToTheLeft = R(nameof(CloseTabsToTheLeft), e.CloseTabsToTheLeft), CloseTabsToTheRight = R(nameof(CloseTabsToTheRight), e.CloseTabsToTheRight),
            SplitRight = R(nameof(SplitRight), e.SplitRight), SplitAndMoveRight = R(nameof(SplitAndMoveRight), e.SplitAndMoveRight),
            SplitDown = R(nameof(SplitDown), e.SplitDown), SplitAndMoveDown = R(nameof(SplitAndMoveDown), e.SplitAndMoveDown),
            PinTab = R(nameof(PinTab), e.PinTab), OpenTabInNewWindow = R(nameof(OpenTabInNewWindow), e.OpenTabInNewWindow),
        };
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

    /// <summary>英文 —— 既是回落，也是 Locales/en-US.axaml 的同一份内容。</summary>
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

    /// <summary>简体中文（与 Locales/zh-CN.axaml 同一份内容；给 <see cref="Override"/> 用）。</summary>
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

    /// <summary>繁体中文（与 Locales/zh-Hant.axaml 同一份内容；给 <see cref="Override"/> 用）。</summary>
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
