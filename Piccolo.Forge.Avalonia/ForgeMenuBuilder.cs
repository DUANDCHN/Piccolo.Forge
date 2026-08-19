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
    /// （<c>ForgeControl.SupportedViewModes</c>）。
    /// </param>
    /// <param name="contents">
    /// 这个工具窗口装的几块内容。给了且**多于一块**时，菜单最前面排出那几块（单选带 ✓）+ 一条分隔线，
    /// 让 ⋮ 也能切块 —— 标题条上那排 tab 在面板窄的时候会被挤掉。只有一块或不给就不出这一段。
    /// </param>
    /// <returns>菜单。</returns>
    public static ContextMenu ForHeaderOptions(
        ToolWindowManager manager,
        string id,
        Action<ContextMenu, string>? extend = null,
        IReadOnlyList<ToolWindowViewMode>? viewModes = null,
        ToolWindowContentManager? contents = null)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(id);

        var menu = Build(
            ToolWindowContextMenu.ForHeaderOptions(viewModes ?? [ToolWindowViewMode.DockPinned]),
            manager, id, static () => { }, showNames: false);
        PrependContents(menu, contents);
        extend?.Invoke(menu, id);
        return menu;
    }

    /// <summary>
    /// 装了多块内容的工具窗口，把那几块排在菜单**最前面** + 一条分隔线。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>为什么补这一段。</b>标题条上那排 tab 在面板窄的时候会被挤掉一部分，
    /// 而 ⋮ 菜单是"这块面板还能干什么"的兜底入口 —— 它原先只列布局相关的三项
    /// （视图模式 / 移至 / 从边栏中移除），**唯独没有"切到哪一块"**。
    /// 编辑区那边的标签菜单一直是有的，两处形态不一致。
    /// </para>
    /// <para>
    /// <b>只有一块时不出</b>：那时"切到哪一块"没有选项可言，多一条分隔线只是噪音。
    /// </para>
    /// <para>
    /// 位置在最前、且在宿主的 <c>extend</c> 之前跑 —— 宿主若也要插到最前
    /// （<c>Insert(0, …)</c>），它的项会排在内容之前。这与上游 <c>additionalGearActions</c>
    /// 的位置一致：宿主的动作最优先，其次才是"这个面板的几块内容"。
    /// </para>
    /// </remarks>
    private static void PrependContents(ContextMenu menu, ToolWindowContentManager? contents)
    {
        if (contents is null || contents.Contents.Count <= 1)
        {
            return;
        }

        var index = 0;
        foreach (var content in contents.Contents)
        {
            var contentId = content.Id;
            var item = Item(content.DisplayName, () => contents.Select(contentId));

            // 单选带 ✓：与「视图模式」那组同一形态 —— 它们都是"当前是哪一个"。
            item.ToggleType = MenuItemToggleType.Radio;
            item.IsChecked = contents.SelectedContent?.Id == contentId;
            menu.Items.Insert(index++, item);
        }

        menu.Items.Insert(index, new Separator());
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
    /// <param name="moveToNewWindow">
    /// 「在新窗口中打开」怎么做；<see langword="null"/> 表示这一项不出现
    /// （嵌在别处的宿主未必有窗口可开）。
    /// </param>
    /// <returns>菜单。</returns>
    public static ContextMenu ForEditorTab(
        EditorAreaViewModel area,
        string tabId,
        bool isPinned,
        Action<ContextMenu, string>? extend = null,
        Action<string>? moveToNewWindow = null)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(tabId);

        var menu = new ContextMenu();
        FillEditorTab(menu, area, tabId, isPinned, extend, moveToNewWindow);
        return menu;
    }

    /// <summary>
    /// 把标签页菜单的内容填进一个**已有**的 <see cref="ContextMenu"/>。
    /// </summary>
    /// <remarks>
    /// 给"菜单实例建一次、每次打开前刷内容"用：每次同步都换一个新菜单实例的话，
    /// 右键按下与松手之间实例就被换掉了，系统那套弹出流程会落空 —— 表现就是右键没反应。
    /// </remarks>
    /// <param name="menu">要填的菜单，会先清空。</param>
    /// <param name="area">编辑区 VM。</param>
    /// <param name="tabId">这一页的 id。</param>
    /// <param name="isPinned">这一页当前是否固定。</param>
    /// <param name="extend">宿主的追加项。</param>
    /// <param name="moveToNewWindow">「在新窗口中打开」怎么做；<see langword="null"/> 表示不摆这一项。</param>
    public static void FillEditorTab(
        ContextMenu menu,
        EditorAreaViewModel area,
        string tabId,
        bool isPinned,
        Action<ContextMenu, string>? extend = null,
        Action<string>? moveToNewWindow = null)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(tabId);

        menu.Items.Clear();
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

                // 与"把标签拖出编辑区"是同一条路（ForgeEditorControl.MoveToNewWindow），
                // 不另开一种"菜单开出来的窗口"。
                case EditorTabMenuItemKind.MoveToNewWindow when moveToNewWindow is not null:
                    menu.Items.Add(Item("在新窗口中打开标签页", () => moveToNewWindow(tabId)));
                    break;
            }
        }

        extend?.Invoke(menu, tabId);
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

    /// <summary>
    /// 拖动时那条落点标签的文案：「移至 左侧 底部」。
    /// </summary>
    /// <remarks>
    /// 对应上游拖影旁边挂的 <c>HelpTooltip</c>（<c>ToolWindowDragHelper.kt:346-352</c>），
    /// 文案是 <c>"Move to " + anchor</c>。这里比菜单里的 <see cref="DescribeMove"/> 说得开一些
    /// ——菜单是一列八项、看得见彼此，"左下"两个字够了；拖动时只有孤零零一条，
    /// 得让人一眼读懂是哪条边的哪一半。
    /// </remarks>
    /// <param name="anchor">落到哪条边。</param>
    /// <param name="isSplit">是不是那条边的「另一半」。</param>
    /// <returns>要显示的字。</returns>
    public static string DescribeMoveTarget(ToolWindowAnchor anchor, bool isSplit)
    {
        var side = anchor switch
        {
            ToolWindowAnchor.Left => "左侧",
            ToolWindowAnchor.Right => "右侧",
            ToolWindowAnchor.Bottom => "底部",
            _ => "顶部",
        };

        // 竖边分上下，横边分左右
        var half = anchor is ToolWindowAnchor.Bottom or ToolWindowAnchor.Top
            ? isSplit ? "右侧" : "左侧"
            : isSplit ? "底部" : "顶部";

        return $"移至 {side} {half}";
    }

    private static MenuItem Item(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }
}
