using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>顶栏菜单：一级菜单之间的切换。</summary>
public class ForgeMenuBarTests
{
    /// <summary>
    /// 菜单开着时，鼠标划到相邻的一级菜单上就切过去。
    /// </summary>
    /// <remarks>
    /// <b>反面（2026-08-26 宿主实机：鼠标划过没反应，只有方向键行）。</b>
    /// 菜单栏那份 popup 开着光消散（<c>IsLightDismissEnabled=True</c>），Avalonia 会铺一层
    /// 遮罩接管指针；要让指针**穿过遮罩落回菜单栏**，popup 必须挂
    /// <c>OverlayInputPassThroughElement</c>（Avalonia 自带主题就是这么写的）。本库漏了它，
    /// 于是遮罩把 PointerEntered 全吞掉 —— 交互处理器收不到，自然不切换。
    /// <para>
    /// ⚠ 判据必须走**真鼠标移动**：这条只在"弹出层开着 + 指针落在菜单栏上"这个组合下成立，
    /// 直接调 API 或只测方向键都照样绿（方向键走的是键盘路径，本来就没被遮罩挡）。
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void 菜单开着时鼠标划到相邻的一级菜单就切过去()
    {
        MenuItem Top(string h) => new()
        {
            Header = h,
            ItemsSource = new[] { new MenuItem { Header = h + "-1" }, new MenuItem { Header = h + "-2" } },
        };
        var items = new[] { Top("文件"), Top("视图"), Top("帮助") };
        var menu = new Menu
        {
            ItemsSource = items,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        var window = new Window { Width = 700, Height = 400, Content = menu };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Point Center(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), window)!.Value;

        window.MouseDown(Center(items[0]), MouseButton.Left);
        window.MouseUp(Center(items[0]), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.True(items[0].IsSubMenuOpen, "点一下该把第一个一级菜单打开");

        foreach (var next in new[] { 1, 2, 0 })
        {
            window.MouseMove(Center(items[next]));
            Dispatcher.UIThread.RunJobs();

            Assert.True(items[next].IsSubMenuOpen, $"鼠标划到「{items[next].Header}」上，它就该展开");
            Assert.All(items.Where(x => x != items[next]), x => Assert.False(x.IsSubMenuOpen));
        }

        window.Close();
    }
}
