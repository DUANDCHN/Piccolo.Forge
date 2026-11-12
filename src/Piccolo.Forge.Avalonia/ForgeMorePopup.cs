using System.Collections.ObjectModel;
using Avalonia.Controls;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 「更多」弹窗：列出条带上没有按钮的工具窗口。
/// </summary>
/// <remarks>
/// 对应 <c>MoreSquareStripeButton.kt:99-121 · ShowMoreToolWindowsAction</c>：
/// 弹出位置见 <see cref="ForgeMoreButton.ComputePopupOffset"/>。
/// <para>
/// <b>它是一份菜单，不是一摞按钮。</b>上游那个动作组弹出来就是常规的动作菜单
/// （图标 + 名字 + 悬停高亮一行）。本库一度用 <see cref="Flyout"/> 里塞
/// <see cref="StackPanel"/> + 一排 <see cref="Button"/> 顶着，两处不像：
/// 每一项都带按钮的边框与内边距，堆起来像三个并排的对话框按钮；
/// 而且 按钮的 <c>Content</c> 直接给的是工具窗口的**内部 id**，
/// 界面上露出的是 <c>ImageCompare</c> 这种开发者才认得的字符串（2026-08-26 宿主实机看出来的）。
/// 现在走 <see cref="MenuFlyout"/> + <see cref="MenuItem"/>：长相由 <c>Forge.MenuItem</c>
/// 那份控件主题给，与右键菜单、顶栏菜单同一套。
/// </para>
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
            .Where(x => x is { IsShowStripeButton: false, IsVisible: false })
            .Select(x => x.Id)
            .ToList();
    }

    /// <summary>
    /// 把「更多」菜单挂到按钮上。
    /// </summary>
    /// <remarks>
    /// <b>一颗按钮只有一份 <see cref="MenuFlyout"/>，条目在每次打开前重建。</b>
    /// <para>
    /// ⚠ 不能"每次点击 new 一份再 ShowAt"（2026-08-26 实机：第一次点得出、第二次点不出）。
    /// 宿主给的图标多半是**控件实例**（<c>ToolWindowViewModel.Icon</c> 是 <c>object?</c>，
    /// LDVision 给的就是一个 SymbolIcon），而一个 Avalonia 控件只能有一个父 ——
    /// 上一份 flyout 的菜单项还攥着它，第二份去挂就抛「已经有父级」，异常在点击处理里
    /// 静默吞掉，表现就是"菜单再也弹不出来"。复用同一份、开之前 <c>Clear</c>，
    /// 图标就在同一时刻只属于一处。
    /// </para>
    /// <para>
    /// ⚠ 走 <see cref="Button.Flyout"/> 而不是自己 <c>Click += ShowAt</c>：菜单开着时再点按钮，
    /// 这一下会被浮层的"点外面关掉"吃掉、按钮的 Click 根本不触发（同日实测：每隔一次才响应）。
    /// 交给 Button 自己管，开合才是一次点击一下。
    /// </para>
    /// </remarks>
    /// <param name="button">条带上那颗「更多」按钮。</param>
    /// <param name="shell">外壳 VM —— 名字与图标都在它那儿，光有 Manager 只拿得到 id。</param>
    public static void Attach(ForgeMoreButton button, ToolWindowShellViewModel shell)
    {
        ArgumentNullException.ThrowIfNull(button);
        ArgumentNullException.ThrowIfNull(shell);

        // ⚠ 条目走 ItemsSource + ObservableCollection，**不是** flyout.Items。
        //   Items 是普通列表：presenter 在弹出层建起来时就把它绑定好了，之后往里增删
        //   **收不到变更通知** —— 菜单照常弹出，但里面一条都没有，屏幕上就是个 2×10 的小块
        //   （2026-08-26 实测：flyout.Items.Count=2 而 presenter 的子项数=0）。
        //   可观察集合才会把重建的结果推给 presenter。
        var items = new ObservableCollection<object>();
        var flyout = new MenuFlyout { ItemsSource = items };
        // 上游 MoreSquareStripeButton.kt:118-121 的 y 对齐按钮上沿；Right 会将菜单按按钮中心居中。
        // 左条带向右、右条带向左展开，仍保留 Avalonia 默认的屏幕边缘翻转与滑动避让。
        void SyncPlacement() => flyout.Placement = ForgeChrome.GetAppearance(button).MorePopupPlacement
            ?? (button.Side == StripeSide.Left ? PlacementMode.RightEdgeAlignedTop : PlacementMode.LeftEdgeAlignedTop);
        SyncPlacement();
        flyout.Opening += (_, _) =>
        {
            // 每次展开读取当前外观，换边或宿主更新配置后也不必重建菜单。
            SyncPlacement();
            Populate(items, flyout, shell);
        };
        button.Flyout = flyout;
    }

    /// <summary>
    /// 按当前布局重建菜单条目。
    /// </summary>
    /// <remarks>
    /// <c>Clear</c> 在前：它把上一批菜单项连同宿主的图标控件一起摘下来，
    /// 下面才挂得上去（理由见 <see cref="Attach"/>）。
    /// </remarks>
    /// <summary>
    /// 菜单项该画什么图标。
    /// </summary>
    /// <remarks>
    /// ⚠ <b>绝不直接用 <c>vm.Icon</c> 的控件实例。</b>一个 Avalonia 控件只能有一个父，
    /// 条带按钮和菜单项同时想挂它，后来的那个当场抛「已经有父级」——
    /// 2026-08-26 两个方向各踩了一次（菜单占着条带拿不回去 / 条带占着菜单挂不上），
    /// 而异常抛在布局测量里，表现是"菜单弹不出来"，看不出跟图标有关。
    /// <para>
    /// 规矩：<see cref="ToolWindowViewModel.IconFactory"/> 给了就**造一个新的**；
    /// 没给就只接受**能共享的**（几何图形、字符这类不可变的东西），是控件就不画。
    /// 宁可少个图标，也不能抛。
    /// </para>
    /// </remarks>
    private static object? MenuIcon(ToolWindowViewModel? vm)
    {
        if (vm is null) return null;
        if (vm.IconFactory?.Invoke() is { } made) return made;
        return vm.Icon is Control ? null : vm.Icon;
    }

    /// <param name="items">菜单的条目集合（可观察，改了 presenter 才跟）。</param>
    /// <param name="flyout">要重建的菜单 —— 条目点中之后要关掉它。</param>
    /// <param name="shell">外壳 VM。</param>
    public static void Populate(ObservableCollection<object> items, MenuFlyout flyout, ToolWindowShellViewModel shell)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(flyout);
        ArgumentNullException.ThrowIfNull(shell);

        foreach (var stale in items.OfType<MenuItem>())
        {
            stale.Icon = null;
        }

        items.Clear();

        foreach (var id in ResolveItems(shell.Manager.Layout))
        {
            var vm = shell.Find(id);
            // ⚠ **不设 MinWidth**，菜单按内容撑。
            //   上游那个 300（ForgeMetrics.MorePopupMinWidth，MoreSquareStripeButton.kt:106）
            //   是给 IntelliJ 那一长串英文工具窗口名定的；本软件的名字是「图像对比」这种四个字，
            //   钉 300 的结果是右边空掉一大片（2026-08-26 宿主实机反馈"有点宽"）。
            //   那个常量本身留着 —— ForgeMoreButton.ComputePopupOffset 还按它算右条带的弹出位置。
            var item = new MenuItem
            {
                Header = vm?.Title ?? id,
                Icon = MenuIcon(vm),
            };

            var toolWindowId = id;
            item.Click += (_, _) =>
            {
                // ⚠ 先把图标从菜单项上摘下来，再把按钮放回条带 —— 同一个控件不能同时
                //   挂在菜单项和条带按钮上。置 null 是同步摘除，所以必须在 Activate 之前。
                item.Icon = null;

                // 从弹窗里选中 = 把按钮放回条带并激活
                var info = shell.Manager.Layout.GetInfo(toolWindowId)!;
                shell.Manager.Layout.SetInfo(info with { IsShowStripeButton = true });
                shell.Manager.Activate(toolWindowId);
                flyout.Hide();
            };

            items.Add(item);
        }
    }
}
