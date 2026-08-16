namespace Piccolo.Forge.Model;

/// <summary>
/// 条带按钮右键菜单里的一项。
/// </summary>
/// <param name="Kind">是哪一项。</param>
/// <param name="Anchor">「移到某边」时的目标边，其余项为 <see langword="null"/>。</param>
/// <param name="IsSplit">「移到某边」时落到哪一半。</param>
/// <param name="Type">「视图模式」时切到哪一种，其余项为 <see langword="null"/>。</param>
public readonly record struct ToolWindowMenuItem(
    ToolWindowMenuItemKind Kind,
    ToolWindowAnchor? Anchor = null,
    bool IsSplit = false,
    ToolWindowType? Type = null);

/// <summary>右键菜单项的种类。</summary>
public enum ToolWindowMenuItemKind
{
    /// <summary>分隔线。</summary>
    Separator,

    /// <summary>隐藏（连条带按钮一起摘掉）。</summary>
    Hide,

    /// <summary>移到某条边。</summary>
    MoveTo,

    /// <summary>切换「显示名字」。</summary>
    ToggleShowNames,

    /// <summary>切换视图模式（停靠固定 / 停靠不固定 / 浮动 / 窗口）。</summary>
    ViewMode,
}

/// <summary>
/// 条带按钮与条带空白处的右键菜单。
/// </summary>
/// <remarks>
/// 对应 <c>SquareStripeButton.kt:381-390 · createPopupGroup</c>
/// 与 <c>ResizeStripeManager.kt:51-59</c>，见 <c>工具窗口模型.md</c> §14.2。
/// </remarks>
public static class ToolWindowContextMenu
{
    /// <summary>
    /// 条带按钮的右键菜单：Hide / 分隔线 / Move to… / 分隔线 / 显示名字。
    /// </summary>
    /// <remarks>
    /// 「移到某边」的八个目标就是 §1.1 那八个格子，只是 TOP 在新 UI 落不了位，
    /// 所以这里只给 LEFT/RIGHT/BOTTOM 六个。
    /// </remarks>
    /// <returns>菜单项。</returns>
    public static IReadOnlyList<ToolWindowMenuItem> ForStripeButton()
    {
        var items = new List<ToolWindowMenuItem> { new(ToolWindowMenuItemKind.Hide), new(ToolWindowMenuItemKind.Separator) };

        foreach (var anchor in new[] { ToolWindowAnchor.Left, ToolWindowAnchor.Right, ToolWindowAnchor.Bottom })
        {
            items.Add(new ToolWindowMenuItem(ToolWindowMenuItemKind.MoveTo, anchor, IsSplit: false));
            items.Add(new ToolWindowMenuItem(ToolWindowMenuItemKind.MoveTo, anchor, IsSplit: true));
        }

        items.Add(new ToolWindowMenuItem(ToolWindowMenuItemKind.Separator));
        items.Add(new ToolWindowMenuItem(ToolWindowMenuItemKind.ToggleShowNames));
        return items;
    }

    /// <summary>
    /// 面板标题条上 ⋮ 的菜单（上游叫 gear popup）。
    /// </summary>
    /// <remarks>
    /// 结构照 <c>ToolWindowImpl.kt:855-888 · GearActionGroup.getChildren</c>，
    /// 只留本模型撑得住的那几组：
    /// <list type="number">
    /// <item><b>视图模式</b>（<c>TW.ViewModeGroup</c>）—— 对应 <see cref="ToolWindowType"/>；</item>
    /// <item><b>移至</b>（<c>SquareStripeButton.createMoveGroup()</c>）—— 与条带右键菜单同一组；</item>
    /// <item>分隔线 + <b>从边栏中移除</b>（<c>RemoveStripeButtonAction</c>）。</item>
    /// </list>
    /// <para>
    /// <b>上游有、这里没有的，逐条说明</b>（宁可不给，也不给一排点不动的项）：
    /// <c>SpeedSearchAction</c>（快速搜索）、<c>CloseAllAction</c>、
    /// <c>toggleToolbarGroup</c>（显示工具栏 / 标签页分组）、<c>HelpAction</c> ——
    /// 这四样都是**内容**那一层的事，库不知道宿主往面板里放了什么，由宿主从扩展点塞；
    /// <c>ResizeActionGroup</c>（调整大小，<c>:982</c>）—— 需要模型层的尺寸步进，
    /// 而上游那几个 action 的步长不在本仓 vendored 的源码里，不臆造，留待补齐；
    /// <c>HideAction</c> —— 已经是标题条上那个「—」按钮，不在菜单里重复一遍。
    /// </para>
    /// </remarks>
    /// <returns>菜单项。</returns>
    public static IReadOnlyList<ToolWindowMenuItem> ForHeaderOptions()
    {
        var items = new List<ToolWindowMenuItem>();

        foreach (var type in new[]
                 {
                     ToolWindowType.Docked, ToolWindowType.Sliding,
                     ToolWindowType.Floating, ToolWindowType.Windowed,
                 })
        {
            items.Add(new ToolWindowMenuItem(ToolWindowMenuItemKind.ViewMode, Type: type));
        }

        foreach (var anchor in new[] { ToolWindowAnchor.Left, ToolWindowAnchor.Right, ToolWindowAnchor.Bottom })
        {
            items.Add(new ToolWindowMenuItem(ToolWindowMenuItemKind.MoveTo, anchor, IsSplit: false));
            items.Add(new ToolWindowMenuItem(ToolWindowMenuItemKind.MoveTo, anchor, IsSplit: true));
        }

        items.Add(new ToolWindowMenuItem(ToolWindowMenuItemKind.Separator));
        items.Add(new ToolWindowMenuItem(ToolWindowMenuItemKind.Hide));
        return items;
    }

    /// <summary>
    /// 条带空白处的右键菜单：只有「显示名字」。
    /// </summary>
    /// <remarks>对应 <c>ResizeStripeManager.kt:51-59</c>。</remarks>
    /// <returns>菜单项。</returns>
    public static IReadOnlyList<ToolWindowMenuItem> ForStripeBackground()
        => [new ToolWindowMenuItem(ToolWindowMenuItemKind.ToggleShowNames)];

    /// <summary>
    /// 执行「隐藏」。
    /// </summary>
    /// <remarks>
    /// 对应 <c>SquareStripeButton.kt:380-388 · HideAction</c>：
    /// 它带的是 <c>removeFromStripe = true</c>，**连条带按钮一起摘掉**，
    /// 不是单纯隐藏面板。
    /// </remarks>
    /// <param name="manager">管理器。</param>
    /// <param name="id">工具窗口 id。</param>
    public static void Hide(ToolWindowManager manager, string id)
    {
        ArgumentNullException.ThrowIfNull(manager);
        manager.Hide(id, hideSide: false, removeFromStripe: true);
    }
}
