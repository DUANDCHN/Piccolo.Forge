using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Piccolo.Forge.Model;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 「更多」弹窗：列出条带上没有按钮的工具窗口。
/// </summary>
/// <remarks>
/// 对应 <c>MoreSquareStripeButton.kt:99-121 · ShowMoreToolWindowsAction</c>：
/// 最小宽 <see cref="ForgeMetrics.MorePopupMinWidth"/>=300，
/// 弹出位置见 <see cref="ForgeMoreButton.ComputePopupOffset"/>。
/// </remarks>
internal static class ForgeMorePopup
{
    /// <summary>
    /// 该在弹窗里列出哪些工具窗口。
    /// </summary>
    /// <remarks>
    /// 上游列的是「可以激活但条带上没有按钮」的那些
    /// （<c>ToolWindowsGroup.getToolWindowActions(project, true)</c>）。
    /// </remarks>
    /// <param name="layout">布局。</param>
    /// <returns>要列出来的工具窗口 id。</returns>
    public static IReadOnlyList<string> ResolveItems(DesktopLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        return layout.GetSortedList()
            .Where(x => !x.IsShowStripeButton && !x.IsVisible)
            .Select(x => x.Id)
            .ToList();
    }

    /// <summary>
    /// 造一个「更多」弹窗。
    /// </summary>
    /// <param name="manager">管理器。</param>
    /// <returns>弹窗；没有可列的东西时返回 <see langword="null"/>。</returns>
    public static Flyout? Create(ToolWindowManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        var items = ResolveItems(manager.Layout);
        if (items.Count == 0)
        {
            return null;
        }

        var panel = new StackPanel { MinWidth = ForgeMetrics.MorePopupMinWidth };
        var flyout = new Flyout { Content = panel, Placement = PlacementMode.Right };

        foreach (var id in items)
        {
            var button = new Button { Content = id, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch };
            var toolWindowId = id;
            button.Click += (_, _) =>
            {
                // 从弹窗里选中 = 把按钮放回条带并激活
                var info = manager.Layout.GetInfo(toolWindowId)!;
                manager.Layout.SetInfo(info with { IsShowStripeButton = true });
                manager.Activate(toolWindowId);
                flyout.Hide();
            };

            panel.Children.Add(button);
        }

        return flyout;
    }
}
