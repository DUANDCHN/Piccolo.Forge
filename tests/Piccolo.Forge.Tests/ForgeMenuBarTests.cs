using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Media;
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

    /// <summary>
    /// 二级子菜单里的字不能跟着父项变白。
    /// </summary>
    /// <remarks>
    /// <b>反面（2026-08-26 宿主实机：亮色主题下二级子菜单"文字不见了"）。</b>
    /// 选中态是把 <c>Foreground</c> 设成白色**设在 MenuItem 自己身上**的，而它的子菜单
    /// 就挂在它的模板里 —— 子项若没有自己的前景色，就会把这个白色继承下去，
    /// 于是白底上一片空白。治法是 <c>Forge.MenuItem</c> 显式给一次前景色，把继承链断在每一项。
    /// <para>
    /// ⚠ 判据落在**画字那一层**（子项里的 TextBlock）而不是 MenuItem 的属性：
    /// 继承来的值在控件上和在字上都看得到，但只有后者是屏幕上真正的颜色。
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void 二级子菜单的字不跟着父项变白()
    {
        var grand = new MenuItem { Header = "以太网" };
        var sub = new MenuItem { Header = "打开目录", ItemsSource = new[] { grand } };
        var top = new MenuItem { Header = "文件", ItemsSource = new[] { sub } };
        var window = new Window { Width = 600, Height = 400, Content = new Menu { ItemsSource = new[] { top } } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        top.IsSubMenuOpen = true;
        Dispatcher.UIThread.RunJobs();
        sub.IsSelected = true;           // 父项高亮：白字蓝底
        sub.IsSubMenuOpen = true;
        Dispatcher.UIThread.RunJobs();

        // 判据不钉具体色值，钉**关系**：三级项的字应当和"没被选中的一级项"同色，
        // 而不是和"被选中的二级项"同色。这样换族、换亮暗、改调色板都不用跟着改这条。
        Color Painted(MenuItem mi) =>
            (mi.GetVisualDescendants().OfType<TextBlock>().First(x => x.Text == (string?)mi.Header).Foreground
                as ISolidColorBrush)!.Color;

        Assert.True(Painted(sub) != Painted(top), "台子没搭对：被选中的二级项本该是高亮色，和常规色不同");
        Assert.Equal(Painted(top), Painted(grand));

        window.Close();
    }
}
