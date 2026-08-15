using Avalonia.Controls;
using Piccolo.Forge.Model;

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
    public static ContextMenu ForStripeButton(ToolWindowManager manager, string id, Action onToggleShowNames)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(onToggleShowNames);

        return Build(ToolWindowContextMenu.ForStripeButton(), manager, id, onToggleShowNames);
    }

    /// <summary>
    /// 条带空白处的右键菜单：只有「显示名字」。
    /// </summary>
    /// <param name="onToggleShowNames">「显示名字」被点了。</param>
    /// <returns>菜单。</returns>
    public static ContextMenu ForStripeBackground(Action onToggleShowNames)
    {
        ArgumentNullException.ThrowIfNull(onToggleShowNames);
        return Build(ToolWindowContextMenu.ForStripeBackground(), manager: null, id: null, onToggleShowNames);
    }

    private static ContextMenu Build(
        IReadOnlyList<ToolWindowMenuItem> items,
        ToolWindowManager? manager,
        string? id,
        Action onToggleShowNames)
    {
        var menu = new ContextMenu();

        foreach (var item in items)
        {
            switch (item.Kind)
            {
                case ToolWindowMenuItemKind.Separator:
                    menu.Items.Add(new Separator());
                    break;

                case ToolWindowMenuItemKind.Hide:
                    menu.Items.Add(Item("隐藏", () => ToolWindowContextMenu.Hide(manager!, id!)));
                    break;

                case ToolWindowMenuItemKind.MoveTo:
                    var anchor = item.Anchor!.Value;
                    var split = item.IsSplit;
                    menu.Items.Add(Item(
                        DescribeMove(anchor, split),
                        () => manager!.MoveTo(id!, anchor, order: -1, isSplit: split)));
                    break;

                case ToolWindowMenuItemKind.ToggleShowNames:
                    menu.Items.Add(Item("显示工具窗口名字", onToggleShowNames));
                    break;
            }
        }

        return menu;
    }

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
