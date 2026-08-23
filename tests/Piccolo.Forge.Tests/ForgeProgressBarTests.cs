using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 进度条的**不确定态**：框架只把短条宽度与起止位置算进 TemplateSettings，
/// 真正让它动起来是主题的活儿。主题不挂动画 = 一条空轨道（不报错、看不见）。
/// </summary>
/// <remarks>判据取"短条有没有宽度"，不取动画进行到第几帧 —— 后者在 headless 下不稳。</remarks>
public class ForgeProgressBarTests
{
    private static Border Indicator(ProgressBar bar) =>
        bar.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_Indicator");

    [AvaloniaFact]
    public void 不确定态的短条要有宽度而不是空轨道()
    {
        var bar = new ProgressBar { IsIndeterminate = true, Width = 200 };
        var window = new Window { Width = 300, Height = 100, Content = bar };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var indicator = Indicator(bar);
        Assert.True(indicator.Bounds.Width > 0, "不确定态的短条宽度为 0 —— 主题没挂动画，进度条整个看不见");
        // 动画跑起来之后 RenderTransform 会被换成动画自己的实例，所以只断言"有位移"，
        // 不断言具体类型。
        Assert.NotNull(indicator.RenderTransform);
        window.Close();
    }

    [AvaloniaFact]
    public void 确定态照旧按值给宽度()
    {
        var bar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 50, Width = 200 };
        var window = new Window { Width = 300, Height = 100, Content = bar };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(Indicator(bar).Bounds.Width > 0);
        window.Close();
    }
}
