using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Piccolo.Forge.Avalonia;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 条带按钮的气泡提示（对位上游 <c>SquareStripeButton.updateHelpTooltip</c>）。
/// </summary>
/// <remarks>
/// 条带按钮默认只有一颗 20px 的图标，**不看提示根本不知道它是什么** —— 所以名字必须进提示。
/// <para>
/// 样式走宿主的默认提示样式，本组不管长相，只钉住「有没有」这件事。
/// 上游那套自成一体的 HelpTooltip 配色试过一版没成（见 <c>ForgeStripeButton</c> 静态构造里那段说明）。
/// </para>
/// </remarks>
public class StripeToolTipTests
{
    [AvaloniaFact]
    public void 名字进了气泡提示()
    {
        var (window, button) = Build();

        Assert.Equal("相机列表", ToolTip.GetTip(button));

        window.Close();
    }

    /// <summary>改名字提示要跟着走 —— 切界面语言时靠的就是这条。</summary>
    [AvaloniaFact]
    public void 改名字提示跟着变()
    {
        var (window, button) = Build();

        button.Title = "Camera List";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Camera List", ToolTip.GetTip(button));

        window.Close();
    }

    /// <summary>名字空着就连提示都不该有：留一个空气泡比没有提示更糟。</summary>
    [AvaloniaFact]
    public void 名字为空时不留空气泡()
    {
        var (window, button) = Build();

        button.Title = "   ";
        Dispatcher.UIThread.RunJobs();

        Assert.Null(ToolTip.GetTip(button));

        window.Close();
    }

    private static (Window Window, ForgeStripeButton Button) Build()
    {
        var window = new Window { Width = 200, Height = 200 };
        var button = new ForgeStripeButton { Title = "相机列表" };
        window.Content = button;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, button);
    }
}
