using Avalonia.Controls;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 把模型算出来的菜单项变成 Avalonia 的菜单。
/// </summary>
/// <remarks>
/// 菜单的内容与语义在模型层（<see cref="ToolWindowContextMenu"/>，出处见
/// <c>工具窗口模型.md</c> §14.2），这里只负责变成控件。
/// </remarks>
public static class ForgeMenuBuilder
{
    /// <summary>
    /// 条带按钮的右键菜单。
    /// </summary>
    /// <param name="manager">管理器。</param>
    /// <param name="id">工具窗口 id。</param>
    /// <param name="onToggleShowNames">「显示名字」被点了。</param>
    /// <returns>菜单。</returns>
    /// <param name="showNames">「显示工具窗口名称」当前是不是开着（决定打不打勾）。</param>
    public static ContextMenu ForStripeButton(
        ToolWindowManager manager,
        string id,
        Action onToggleShowNames,
        bool showNames = false)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(onToggleShowNames);

        return Build(ToolWindowContextMenu.ForStripeButton(), manager, id, onToggleShowNames, showNames);
    }

    /// <summary>
    /// 面板标题条上 ⋮ 的菜单。
    /// </summary>
    /// <remarks>
    /// 结构见 <see cref="ToolWindowContextMenu.ForHeaderOptions"/>（照 <c>ToolWindowImpl.kt:855-888</c>）：
    /// 视图模式 ▸ / 移至 ▸ / 分隔线 / 从边栏中移除。
    /// <para>
    /// 与条带按钮的右键菜单**是两份**：那份是"这个按钮"的菜单（隐藏、移动、显示名字），
    /// 这份是"这个面板"的菜单（视图模式、移动、从边栏移除）。上游也是两份。
    /// </para>
    /// </remarks>
    /// <param name="manager">工具窗口管理器。</param>
    /// <param name="id">哪个工具窗口。</param>
    /// <param name="extend">
    /// 宿主往里追加自己的项。**菜单原样交给你**：<c>Add</c> 追加在后面，
    /// 想要上游那个位置（<c>additionalGearActions</c> 在最前 + 一条分隔线）就自己
    /// <c>Insert(0, …)</c>。与 <c>TabMenuExtension</c> / <c>TabActionsMenuExtension</c> 同形 ——
    /// 三个扩展点行为一致，宿主不用记「哪个会把我的项搬走」。
    /// </param>
    /// <param name="viewModes">
    /// 「视图模式」里列哪几种；不给就只列停靠固定。视图层应当只给它**画得出来**的那些
    /// （<c>ForgeToolWindowFrame.SupportedViewModes</c>）。
    /// </param>
    /// <returns>菜单。</returns>
    public static ContextMenu ForHeaderOptions(
        ToolWindowManager manager,
        string id,
        Action<ContextMenu, string>? extend = null,
        IReadOnlyList<ToolWindowViewMode>? viewModes = null)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(id);

        var menu = Build(
            ToolWindowContextMenu.ForHeaderOptions(viewModes ?? [ToolWindowViewMode.DockPinned]),
            manager, id, static () => { }, showNames: false);
        extend?.Invoke(menu, id);
        return menu;
    }

    /// <summary>
    /// 条带空白处的右键菜单：只有「显示名字」。
    /// </summary>
    /// <param name="onToggleShowNames">「显示名字」被点了。</param>
    /// <returns>菜单。</returns>
    /// <param name="showNames">「显示工具窗口名称」当前是不是开着（决定打不打勾）。</param>
    public static ContextMenu ForStripeBackground(Action onToggleShowNames, bool showNames = false)
    {
        ArgumentNullException.ThrowIfNull(onToggleShowNames);
        return Build(
            ToolWindowContextMenu.ForStripeBackground(), manager: null, id: null, onToggleShowNames, showNames);
    }

    private static ContextMenu Build(
        IReadOnlyList<ToolWindowMenuItem> items,
        ToolWindowManager? manager,
        string? id,
        Action onToggleShowNames,
        bool showNames)
    {
        var menu = new ContextMenu();

        // 「移至」是**一个子菜单**，不是把 8 个位置平铺在一级里 ——
        // 上游那份就是 Move to ▸（用户实测截图亦然）。平铺会得到一条 11 项的长菜单，
        // 而这 8 项本来就是同一件事的取值。
        MenuItem? moveTo = null;
        MenuItem? viewMode = null;

        foreach (var item in items)
        {
            switch (item.Kind)
            {
                case ToolWindowMenuItemKind.Separator:
                    menu.Items.Add(new Separator());
                    break;

                case ToolWindowMenuItemKind.Hide:
                    // 名字照上游的 RemoveStripeButtonAction —— 它做的就是「连条带按钮一起摘掉」，
                    // 叫「隐藏」会与标题条上那个「—」（只收起、按钮还在）混成一件事。
                    menu.Items.Add(Item("从边栏中移除", () => ToolWindowContextMenu.Hide(manager!, id!)));
                    break;

                case ToolWindowMenuItemKind.ViewMode:
                    var wanted = item.ViewMode!.Value;

                    if (viewMode is null)
                    {
                        viewMode = new MenuItem { Header = "视图模式" };
                        menu.Items.Add(viewMode);
                    }

                    // 可勾选：当前是哪一种要一眼看得出（上游那组是单选带 ✓）
                    var mode = Item(
                        DescribeViewMode(wanted),
                        () => ToolWindowViewModes.Apply(manager!, id!, wanted));
                    mode.ToggleType = MenuItemToggleType.Radio;
                    mode.IsChecked = manager?.Layout.GetInfo(id!) is { } current
                                     && ToolWindowViewModes.Resolve(current) == wanted;
                    viewMode.Items.Add(mode);
                    break;

                case ToolWindowMenuItemKind.MoveTo:
                    var anchor = item.Anchor!.Value;
                    var split = item.IsSplit;

                    if (moveTo is null)
                    {
                        moveTo = new MenuItem { Header = "移至" };
                        menu.Items.Add(moveTo);
                    }

                    moveTo.Items.Add(Item(
                        DescribeMove(anchor, split),
                        () => manager!.MoveTo(id!, anchor, order: -1, isSplit: split)));
                    break;

                case ToolWindowMenuItemKind.ToggleShowNames:
                    // 可勾选：不给勾的话，用户看不出当前开没开（上游那项是带 ✓ 的）
                    var toggle = Item("显示工具窗口名称", onToggleShowNames);
                    toggle.ToggleType = MenuItemToggleType.CheckBox;
                    toggle.IsChecked = showNames;
                    menu.Items.Add(toggle);
                    break;
            }
        }

        return menu;
    }

    /// <summary>
    /// 编辑区标签页的右键菜单。
    /// </summary>
    /// <remarks>
    /// 菜单**只含编辑区自己的语义**，理由见 <see cref="EditorTabContextMenu"/>。
    /// 宿主要加自己的项（另存为、在资源管理器中显示、版本控制…）走
    /// <paramref name="extend"/>，不要往库里塞。
    /// </remarks>
    /// <param name="area">编辑区 VM。</param>
    /// <param name="tabId">这一页的 id。</param>
    /// <param name="isPinned">这一页当前是否固定（决定「固定 / 取消固定」的文案与勾选）。</param>
    /// <param name="extend">宿主的追加项；<see langword="null"/> 表示不追加。</param>
    /// <returns>菜单。</returns>
    public static ContextMenu ForEditorTab(
        EditorAreaViewModel area,
        string tabId,
        bool isPinned,
        Action<ContextMenu, string>? extend = null)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(tabId);

        var menu = new ContextMenu();
        var model = area.Area;

        foreach (var item in EditorTabContextMenu.Standard())
        {
            switch (item.Kind)
            {
                case EditorTabMenuItemKind.Separator:
                    menu.Items.Add(new Separator());
                    break;

                case EditorTabMenuItemKind.Close:
                    menu.Items.Add(Item("关闭", () => model.Close(tabId)));
                    break;

                case EditorTabMenuItemKind.CloseOthers:
                    menu.Items.Add(Item("关闭其他标签页", () => model.CloseOthers(tabId)));
                    break;

                case EditorTabMenuItemKind.CloseAll:
                    menu.Items.Add(Item("关闭所有标签页", () => model.CloseAll(tabId)));
                    break;

                case EditorTabMenuItemKind.CloseToTheLeft:
                    menu.Items.Add(Item("关闭左侧标签页", () => model.CloseToTheLeft(tabId)));
                    break;

                case EditorTabMenuItemKind.CloseToTheRight:
                    menu.Items.Add(Item("关闭右侧标签页", () => model.CloseToTheRight(tabId)));
                    break;

                // 「拆分」把选中那页留在原组、新组开一份；「拆分并移动」是把它搬过去。
                // 两者都落到 EditorArea.Split —— 区别只在事后要不要把原组那份关掉。
                case EditorTabMenuItemKind.SplitRight:
                    menu.Items.Add(Item("向右拆分", () => Split(model, tabId, vertical: false, move: false)));
                    break;

                case EditorTabMenuItemKind.SplitAndMoveRight:
                    menu.Items.Add(Item("拆分并右移", () => Split(model, tabId, vertical: false, move: true)));
                    break;

                case EditorTabMenuItemKind.SplitDown:
                    menu.Items.Add(Item("向下拆分", () => Split(model, tabId, vertical: true, move: false)));
                    break;

                case EditorTabMenuItemKind.SplitAndMoveDown:
                    menu.Items.Add(Item("拆分并下移", () => Split(model, tabId, vertical: true, move: true)));
                    break;

                case EditorTabMenuItemKind.TogglePinned:
                    var pin = Item("固定标签页", () => model.SetPinned(tabId, !isPinned));
                    pin.ToggleType = MenuItemToggleType.CheckBox;
                    pin.IsChecked = isPinned;
                    menu.Items.Add(pin);
                    break;
            }
        }

        extend?.Invoke(menu, tabId);
        return menu;
    }

    /// <summary>拆分，并按需要把原组那份收掉。</summary>
    /// <remarks>
    /// <c>EditorArea.Split</c> 的语义是「只把选中那一个带过去，原组不动」
    /// （<c>EditorWindow.kt:570-571</c>），所以「拆分并移动」= 拆完再把原组那份关掉。
    /// ⚠ 关的时候要认准**原来那一组**：拆分之后两组都有同 id 的页，
    /// 直接 <c>Close(id)</c> 会关到哪一个不确定。
    /// </remarks>
    private static void Split(EditorArea area, string tabId, bool vertical, bool move)
    {
        var source = area.Groups.FirstOrDefault(g => g.Tabs.Any(t => t.Id == tabId));
        if (source is null)
        {
            return;
        }

        if (area.Split(source, vertical, tabId) is null || !move)
        {
            return;
        }

        source.Close(tabId);
        area.Select(tabId);
    }

    /// <summary>视图模式的中文名（对应 <c>TW.ViewModeGroup</c> 那几项）。</summary>
    /// <remarks>
    /// 五项齐了。曾经缺「取消停靠」，是因为把它和 SLIDING 混成了「停靠不固定」——
    /// 上游这两件事分在 <c>type</c> 与 <c>isAutoHide</c> 两个字段上，见 <see cref="ToolWindowViewMode"/>。
    /// </remarks>
    /// <param name="mode">视图模式。</param>
    /// <returns>菜单上显示的名字。</returns>
    private static string DescribeViewMode(ToolWindowViewMode mode) => mode switch
    {
        ToolWindowViewMode.DockPinned => "停靠固定",
        ToolWindowViewMode.DockUnpinned => "停靠不固定",
        ToolWindowViewMode.Undock => "取消停靠",
        ToolWindowViewMode.Float => "浮动",
        ToolWindowViewMode.Window => "窗口",
        _ => mode.ToString(),
    };

    /// <summary>
    /// 「移到…」这一项该怎么念。
    /// </summary>
    /// <remarks>
    /// 名字取自上游的位置图标（<c>ToolWindowManagerImpl.kt:770-786 · getLocationIcon</c>）：
    /// 竖边的另一半在**下**，横边的另一半在**右**。
    /// </remarks>
    /// <param name="anchor">目标边。</param>
    /// <param name="isSplit">是不是另一半。</param>
    /// <returns>菜单项的名字。</returns>
    public static string DescribeMove(ToolWindowAnchor anchor, bool isSplit) => anchor switch
    {
        ToolWindowAnchor.Left => isSplit ? "左下" : "左上",
        ToolWindowAnchor.Right => isSplit ? "右下" : "右上",
        ToolWindowAnchor.Bottom => isSplit ? "下右" : "下左",
        _ => isSplit ? "上右" : "上左",
    };

    private static MenuItem Item(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }
}
