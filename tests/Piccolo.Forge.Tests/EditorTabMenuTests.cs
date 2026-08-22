using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Linq;
using Avalonia.Headless.XUnit;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 编辑区标签页的右键菜单：只含编辑区自己的语义，其余靠宿主追加。
/// </summary>
public class EditorTabMenuTests
{
    private static EditorAreaViewModel NewArea()
    {
        var area = new EditorAreaViewModel();
        area.Open("a", "A.cs", "A");
        area.Open("b", "B.cs", "B");
        area.Open("c", "C.cs", "C");
        return area;
    }

    [AvaloniaFact]
    public void The_menu_only_carries_editor_semantics()
    {
        var area = NewArea();
        var menu = ForgeMenuBuilder.ForEditorTab(area, "b", isPinned: false, moveToNewWindow: _ => { });

        var headers = menu.Items.OfType<MenuItem>().Select(x => (string?)x.Header).ToList();

        Assert.Equal(
            ["关闭", "关闭其他标签页", "关闭所有标签页", "关闭左侧标签页", "关闭右侧标签页",
             "向右拆分", "拆分并右移", "向下拆分", "拆分并下移", "固定标签页", "在新窗口中打开标签页"],
            headers);

        // ⚠ 刻意**不含** Git / 书签 / 本地历史 / 复制路径 / 重命名 —— 那些依赖宿主能力，
        //    让库去建模它们是错的分层（见 EditorTabContextMenu 的说明）。
        Assert.DoesNotContain(headers, x => x is not null && (x.Contains("Git") || x.Contains("书签")));
    }

    /// <summary>
    /// 「在新窗口中打开」得有人来做才出现 —— 嵌在别处的宿主未必有窗口可开。
    /// </summary>
    [AvaloniaFact]
    public void 没人接管新窗口时就不摆这一项()
    {
        var area = NewArea();
        var menu = ForgeMenuBuilder.ForEditorTab(area, "b", isPinned: false);

        Assert.DoesNotContain(
            menu.Items.OfType<MenuItem>().Select(x => (string?)x.Header),
            x => x == "在新窗口中打开标签页");
    }

    /// <summary>
    /// 每个标签容器身上都得挂着右键菜单 —— 否则右键什么都不弹。
    /// </summary>
    /// <remarks>
    /// 回归：菜单只在同步那一轮配一次，而那一刻容器**可能还没实现出来**
    /// （刚给 ItemsSource 的那轮尤其如此），赋值落空后就再没人补 ——
    /// 标题之类后来还会被别的同步重刷，菜单不会。现在容器一实现就配。
    /// <para>
    /// 这里不断言"右键之后菜单打开了"：headless 平台不发 ContextRequested
    /// （那是系统手势），断言得到的只会是假绿。
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void 标签容器上必须挂着右键菜单()
    {
        var area = NewArea();
        var control = new ForgeEditorControl();
        control.Attach(area);

        var window = new Window { Width = 600, Height = 400, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var tabs = window.GetVisualDescendants().OfType<ForgeEditorTab>().ToList();
        Assert.NotEmpty(tabs);
        Assert.All(tabs, tab => Assert.NotNull(tab.ContextMenu));

        window.Close();
    }

    [AvaloniaFact]
    public void The_host_can_append_its_own_items()
    {
        var area = NewArea();
        var menu = ForgeMenuBuilder.ForEditorTab(area, "b", isPinned: false, (m, id) =>
        {
            m.Items.Add(new Separator());
            m.Items.Add(new MenuItem { Header = $"另存为…（{id}）" });
        });

        Assert.Contains(menu.Items.OfType<MenuItem>(), x => (string?)x.Header == "另存为…（b）");
    }

    /// <summary>「固定」是可勾选的，勾要反映当前状态。</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_pin_item_shows_its_current_state(bool pinned)
    {
        var area = NewArea();
        var menu = ForgeMenuBuilder.ForEditorTab(area, "b", pinned);
        var pin = Assert.Single(menu.Items.OfType<MenuItem>(), x => (string?)x.Header == "固定标签页");

        Assert.Equal(MenuItemToggleType.CheckBox, pin.ToggleType);
        Assert.Equal(pinned, pin.IsChecked);
    }

    /// <summary>关闭系列跳过固定住的那些（固定的语义就是「别动我」）。</summary>
    [AvaloniaFact]
    public void Closing_others_skips_pinned_tabs()
    {
        var area = NewArea();
        area.Area.SetPinned("a", pinned: true);

        Assert.Equal(1, area.Area.CloseOthers("b"));   // 只关掉了 c，a 被跳过

        var ids = area.Area.Groups.SelectMany(g => g.Tabs).Select(t => t.Id).ToList();
        Assert.Equal(["a", "b"], ids);
    }

    [AvaloniaFact]
    public void Closing_to_the_left_and_right_uses_display_order()
    {
        var area = NewArea();
        Assert.Equal(1, area.Area.CloseToTheLeft("b"));
        Assert.Equal(["b", "c"], area.Area.Groups.SelectMany(g => g.Tabs).Select(t => t.Id));

        Assert.Equal(1, area.Area.CloseToTheRight("b"));
        Assert.Equal(["b"], area.Area.Groups.SelectMany(g => g.Tabs).Select(t => t.Id));
    }

    /// <summary>
    /// 打开面板时，**视图在收到通知那一刻就该看得到图标**。
    /// </summary>
    /// <remarks>
    /// 回归：Open(IEditorPanel) 原来是先 Area.Open（触发 Changed、视图当场把标签页画出来）、
    /// 之后才给 vm.Icon 赋值。给 VM 设 Icon **不触发 Area.Changed**，视图不会回头再读，
    /// 于是新开的文档没图标；切一下文档（Select → Changed）图标才冒出来。
    /// <para>这里在 Changed 回调里当场检查图标，正好覆盖那个时序。</para>
    /// </remarks>
    [AvaloniaFact]
    public void The_icon_is_already_there_when_the_change_is_announced()
    {
        var area = new EditorAreaViewModel();
        object? iconAtNotifyTime = null;
        var notified = false;

        area.Area.Changed += (_, _) =>
        {
            if (notified)
            {
                return;
            }

            notified = true;
            iconAtNotifyTime = area.Groups.SelectMany(g => g.Tabs).FirstOrDefault(x => x.Id == "cam-1")?.Icon;
        };

        var icon = new Border();
        area.Open(new StubPanel("cam-1", "相机 1", icon));

        Assert.True(notified, "Area.Open 应当触发 Changed");
        Assert.Same(icon, iconAtNotifyTime);
    }

    private sealed class StubPanel(string id, string title, object? icon) : IEditorPanel
    {
        public string Id { get; set; } = id;

        public string Title { get; set; } = title;

        public object? Icon { get; set; } = icon;
    }
}
