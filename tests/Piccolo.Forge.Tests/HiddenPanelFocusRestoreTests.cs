using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 面板内控件持有焦点时经菜单「隐藏」该面板：菜单弹出先拿走焦点，关闭时把焦点还回原控件。
/// 隐藏后的视觉树同步被推迟到本轮输入之后（见 ForgeControl.QueueSync），还焦点那一刻原控件仍可见、能拿到焦点；
/// 此时不得因"焦点进了这个面板"而把刚隐藏的面板重新激活、重新显示。
/// </summary>
public class HiddenPanelFocusRestoreTests
{
    [AvaloniaFact]
    public void Focus_returning_to_a_just_hidden_panel_does_not_reopen_it()
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var frame = new ForgeControl();
        frame.Attach(shell);
        var inPanel = new Button { Content = "plot", Focusable = true };
        shell.Find("Project")!.Content = inPanel;
        shell.Open("Project");
        var elsewhere = new Button { Content = "menu", Focusable = true };
        var root = new DockPanel();
        DockPanel.SetDock(elsewhere, Dock.Top);
        root.Children.Add(elsewhere);
        root.Children.Add(frame);
        var window = new Window { Width = 1000, Height = 700, Content = root };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(inPanel.Focus());
        Dispatcher.UIThread.RunJobs();

        // 菜单弹出：焦点离开面板。
        elsewhere.Focus();
        // 菜单项执行「隐藏」。
        ToolWindowContextMenu.Hide(shell.Manager, "Project");
        // 菜单关闭：焦点还回之前的控件 —— 发生在推迟的视觉树同步之前。
        inPanel.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.False(shell.Manager.Layout.GetInfo("Project")!.IsVisible, "焦点还回刚隐藏的面板里的控件后，面板被重新打开了");
        window.Close();
    }
}
