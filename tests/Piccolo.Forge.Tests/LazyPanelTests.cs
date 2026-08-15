using Avalonia;
using System.Linq;
using Avalonia.Threading;
using Avalonia.Input;
using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Mvvm.Controls;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 登记与「造实例」是两件事：按钮全都在，重面板可以晚点造。
public class LazyPanelTests
{
    private sealed class HeavyTool : ToolWindowPanel
    {
        public static int Constructed { get; private set; }

        public HeavyTool(string id) : base(id) => Constructed++;

        public static void ResetCounter() => Constructed = 0;
    }

    [Fact]
    public void Registering_needs_only_id_title_and_icon()
    {
        // 登记不需要面板实例 —— 这是「按钮全都在，但重面板不必启动就造」的前提
        HeavyTool.ResetCounter();
        var shell = new ToolWindowShellViewModel();

        shell.Add("Histogram", "直方图", ToolWindowAnchor.Bottom, isSplit: true, icon: "H");

        Assert.Equal(0, HeavyTool.Constructed);
        Assert.NotNull(shell.Find("Histogram"));
        Assert.True(shell.Find("Histogram")!.IsShowStripeButton);
    }

    [AvaloniaFact]
    public void An_unopened_panel_still_gets_a_stripe_button()
    {
        var shell = new ToolWindowShellViewModel();
        shell.Add("Histogram", "直方图", ToolWindowAnchor.Bottom, isSplit: true, icon: "H");

        var frame = new ForgeToolWindowFrame();
        frame.Attach(shell);

        // anchor=Bottom + isSplit=true → 右条带的下半组（§1.2）
        Assert.Single(frame.RightToolbar.BottomGroup.Children.OfType<ForgeStripeButton>());
        Assert.False(shell.Find("Histogram")!.IsOpen);
    }

    [Fact]
    public void The_content_can_be_attached_later()
    {
        // 第一次打开时才造实例，然后挂上去
        HeavyTool.ResetCounter();
        var shell = new ToolWindowShellViewModel();
        shell.Add("Histogram", "直方图", ToolWindowAnchor.Bottom);
        var vm = shell.Find("Histogram")!;
        Assert.Null(vm.Content);

        vm.Content = new HeavyTool("Histogram");
        shell.Open("Histogram");

        Assert.Equal(1, HeavyTool.Constructed);
        Assert.True(vm.IsOpen);
    }

    [AvaloniaFact]
    public void Every_registered_panel_has_a_button_no_matter_the_edge()
    {
        // 「全部要有按钮」：登记了就有，不用先打开、也不用先造实例
        var shell = new ToolWindowShellViewModel();
        shell.Add("Project", "项目", ToolWindowAnchor.Left);
        shell.Add("CamParam", "相机参数", ToolWindowAnchor.Right);
        shell.Add("Log", "日志", ToolWindowAnchor.Bottom);
        shell.Add("Histogram", "直方图", ToolWindowAnchor.Bottom, isSplit: true);

        var frame = new ForgeToolWindowFrame();
        frame.Attach(shell);

        Assert.Single(frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>());
        Assert.Single(frame.RightToolbar.TopGroup.Children.OfType<ForgeStripeButton>());
        Assert.Single(frame.LeftToolbar.BottomGroup.Children.OfType<ForgeStripeButton>());
        Assert.Single(frame.RightToolbar.BottomGroup.Children.OfType<ForgeStripeButton>());
    }

    [AvaloniaFact]
    public void There_is_no_bottom_stripe_at_all()
    {
        // 上游新 UI 只有左右两根条带：getStripeHeight 恒返回 0
        // （ToolWindowPaneNewButtonManager.kt:160-167）。
        // anchor=Bottom 的按钮画在**左/右条带的下半组**，不是画在窗口底边。
        var shell = new ToolWindowShellViewModel();
        shell.Add("Log", "日志", ToolWindowAnchor.Bottom);

        var frame = new ForgeToolWindowFrame();
        frame.Attach(shell);
        frame.Measure(new Size(800, 600));
        frame.Arrange(new Rect(0, 0, 800, 600));

        var button = Assert.Single(frame.LeftToolbar.BottomGroup.Children.OfType<ForgeStripeButton>());
        Assert.Equal("日志", button.Title);
    }

    [Fact]
    public void The_factory_runs_on_first_use_not_at_registration()
    {
        HeavyTool.ResetCounter();
        var shell = new ToolWindowShellViewModel();

        var vm = shell.Add("Histogram", "直方图", ToolWindowAnchor.Bottom,
            () => new HeavyTool("Histogram"), isSplit: true);

        Assert.Equal(0, HeavyTool.Constructed);
        Assert.False(vm.IsContentCreated);

        _ = vm.Content;

        Assert.Equal(1, HeavyTool.Constructed);
        Assert.True(vm.IsContentCreated);
    }

    [Fact]
    public void The_factory_runs_only_once()
    {
        HeavyTool.ResetCounter();
        var shell = new ToolWindowShellViewModel();
        var vm = shell.Add("Histogram", "直方图", ToolWindowAnchor.Bottom, () => new HeavyTool("Histogram"));

        _ = vm.Content;
        _ = vm.Content;
        _ = vm.Content;

        Assert.Equal(1, HeavyTool.Constructed);
    }

    [AvaloniaFact]
    public void Opening_from_the_stripe_button_fills_the_panel()
    {
        // 回归：用户点条带按钮走的是**库内部**的开合，宿主插不上手。
        // 没有工厂的话，那条路上没人创建内容 —— 按钮点了、面板开了、里面是空的。
        HeavyTool.ResetCounter();
        var shell = new ToolWindowShellViewModel();
        shell.Add("Histogram", "直方图", ToolWindowAnchor.Bottom, () => new HeavyTool("Histogram"));

        var frame = new ForgeToolWindowFrame();
        frame.Attach(shell);

        var window = new Window { Width = 600, Height = 400, Content = frame };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, HeavyTool.Constructed);

        var button = frame.LeftToolbar.BottomGroup.Children.OfType<ForgeStripeButton>().First();
        var point = button.TranslatePoint(new Point(20, 20), frame)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.True(shell.Find("Histogram")!.IsOpen);
        Assert.Equal(1, HeavyTool.Constructed);
        Assert.IsType<HeavyTool>(shell.Find("Histogram")!.Content);
    }
}
