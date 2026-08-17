using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 这一组是 Mvvm 层存在的理由：开关面板不能把别人的内容重造一遍。
public class ContentReuseTests
{
    /// 一个会数自己被造了几次的假内容，模拟相机画面 / 图表这类有状态的东西。
    private sealed class LiveContent : Control
    {
        public static int Constructed { get; private set; }

        public LiveContent() => Constructed++;

        public int Frames { get; set; }

        public static void ResetCounter() => Constructed = 0;
    }

    [AvaloniaFact]
    public void Toggling_another_panel_does_not_recreate_a_live_content_control()
    {
        var (frame, shell) = Build();
        LiveContent.ResetCounter();

        var live = new LiveContent { Frames = 42 };
        shell.Find("Project")!.Content = live;
        shell.Open("Project");

        // 开关别的面板若干次
        for (var i = 0; i < 3; i++)
        {
            shell.Open("Terminal");
            shell.Close("Terminal");
        }

        Assert.Equal(1, LiveContent.Constructed);
        Assert.Equal(42, live.Frames);
        Assert.Same(live, FindContentHost(frame, "Project").Content);
    }

    [AvaloniaFact]
    public void The_panel_host_itself_survives_a_close_and_reopen()
    {
        // 关掉再打开，宿主控件应当是同一个 —— 否则滚动位置、选中态这些都会丢
        var (frame, shell) = Build();
        shell.Open("Project");
        var host = FindContentHost(frame, "Project");

        shell.Close("Project");
        shell.Open("Project");

        Assert.Same(host, FindContentHost(frame, "Project"));
    }

    [AvaloniaFact]
    public void The_stripe_button_is_reused_when_the_window_moves()
    {
        var (frame, shell) = Build();
        var button = frame.LeftToolbar.BottomGroup.Children
            .OfType<ForgeStripeButton>()
            .Single(x => ((ToolWindowViewModel)x.Tag!).Id == "Terminal");

        shell.MoveTo("Terminal", ToolWindowAnchor.Bottom, isSplit: true);

        var moved = frame.RightToolbar.BottomGroup.Children
            .OfType<ForgeStripeButton>()
            .Single(x => ((ToolWindowViewModel)x.Tag!).Id == "Terminal");

        Assert.Same(button, moved);
        Assert.DoesNotContain(button, frame.LeftToolbar.BottomGroup.Children);
    }

    [AvaloniaFact]
    public void Content_is_an_object_so_the_host_resolves_the_view()
    {
        // 控件库不认识宿主的视图：塞个纯 VM 进去，ContentControl 交给宿主的模板去解析
        var (frame, shell) = Build();
        var viewModel = new { Title = "我是宿主的 VM" };

        shell.Find("Project")!.Content = viewModel;
        shell.Open("Project");

        Assert.Same(viewModel, FindContentHost(frame, "Project").Content);
    }

    private static ContentControl FindContentHost(ForgeControl frame, string id)
    {
        var header = frame.DockPane.GetVisualDescendants()
            .OfType<ForgeToolWindowHeader>()
            .Single(x => x.Title == id);

        // 面板宿主的结构是 Border > DockPanel > [Header, ContentControl]
        var body = (DockPanel)header.Parent!;
        return body.Children.OfType<ContentControl>().Single();
    }

    private static (ForgeControl Frame, ToolWindowShellViewModel Shell) Build()
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var frame = new ForgeControl();
        frame.Attach(shell);
        return (frame, shell);
    }
}
