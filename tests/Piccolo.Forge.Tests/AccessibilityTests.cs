using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 无障碍：只有图标的按钮与标签要有 <c>AutomationProperties.Name</c>，读屏器才念得出"这是哪个面板 / 哪个按钮"。
/// </summary>
public class AccessibilityTests
{
    [AvaloniaFact]
    public void Stripe_buttons_are_named_after_their_tool_window()
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var frame = new ForgeControl();
        frame.Attach(shell);
        new Window { Content = frame }.Show();

        var vm = shell.Find("Project")!;
        var button = frame.GetVisualDescendants().OfType<ForgeStripeButton>().Single(b => ReferenceEquals(b.Tag, vm));
        Assert.Equal(vm.Title, AutomationProperties.GetName(button));

        vm.Title = "Projekt";
        Assert.Equal("Projekt", AutomationProperties.GetName(button));
    }

    [AvaloniaFact]
    public void Header_builtin_buttons_and_more_button_have_names()
    {
        var header = new ForgeToolWindowHeader();
        Assert.Equal(ForgeStrings.Current.OptionsButton, AutomationProperties.GetName(header.OptionsButton));

        var more = new ForgeMoreButton();
        Assert.Equal(ForgeStrings.Current.MoreToolWindows, AutomationProperties.GetName(more));
    }

    [AvaloniaFact]
    public void Tabs_are_named_after_their_title()
    {
        var tab = new ForgeEditorTab { Title = "Main.cs" };
        Assert.Equal("Main.cs", AutomationProperties.GetName(tab));

        var content = new ForgeContentTab { Title = "Basic" };
        Assert.Equal("Basic", AutomationProperties.GetName(content));
    }
}
