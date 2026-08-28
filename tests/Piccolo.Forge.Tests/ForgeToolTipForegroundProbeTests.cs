using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 宿主控件的前景色不会渗进它的提示气泡。
/// </summary>
/// <remarks>
/// 由来（2026-08-28）：宿主报"工具条上只有当前选中的那颗，气泡里的字看不见"。选中态与常态的唯一
/// 差别是 <c>Forge.ToggleButton</c> 的 <c>^:checked</c> 把控件自己的 <c>Foreground</c> 设成了浅色，
/// 而 <c>Foreground</c> 可继承、气泡在逻辑树上又是宿主的子 —— 于是怀疑浅底气泡吃到了浅字。
/// <b>这条用例证伪了那个怀疑</b>：气泡的字色确实只认自己的主题。留着当回归，
/// 顺便挡住"哪天给气泡去掉 Foreground setter 改成继承"这种改法。
/// <para>
/// ⚠ <b>必须在 Islands 族下测</b>：ExpUi 两档都是深底白字，跟选中态宿主的白前景撞成同一个颜色，
/// 在那族下断言恒成立、一点判别力都没有（第一版就是这么假绿的）。
/// </para>
/// </remarks>
public class ForgeToolTipForegroundProbeTests
{
    /// <summary>
    /// <b>气泡的字色只认气泡自己的主题，不跟宿主走。</b>
    /// <para>
    /// 宿主可能因为任何原因把自己的前景改成浅色（选中态、强调色、彩色图标钮），那是"控件长什么样"，
    /// 与"气泡里的字该是什么颜色"无关 —— 气泡有自己的底，字色必须跟着那个底走，
    /// 否则总有一种宿主状态会把字变没。
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void 宿主前景色不渗进提示气泡()
    {
        using var theme = ForgeThemeScope.Use(ForgeThemeFamily.Islands);

        var host = new ToggleButton { IsChecked = true, Content = "宿主" };
        var window = new Window
        {
            Width = 300,
            Height = 200,
            Content = host,
            RequestedThemeVariant = ThemeVariant.Light,
        };
        ToolTip.SetTip(host, "测距");

        window.Show();
        Relayout(window);

        // 选中态确实把宿主自己染成了浅色 —— 这是本用例要防的那个前提，不成立就没什么好防的。
        var hostForeground = ColorOf(host.Foreground);
        Assert.Equal(Colors.White, hostForeground);

        ToolTip.SetIsOpen(host, true);
        Relayout(window);

        var tip = window.GetVisualDescendants().OfType<ToolTip>().Single();
        var text = tip.GetVisualDescendants().OfType<TextBlock>().First();

        Assert.NotEqual(hostForeground, ColorOf(text.Foreground));
        Assert.Equal(ColorOf(tip.Foreground), ColorOf(text.Foreground));

        ToolTip.SetIsOpen(host, false);
        window.Close();
    }

    private static Color? ColorOf(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    private static void Relayout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
    }
}
