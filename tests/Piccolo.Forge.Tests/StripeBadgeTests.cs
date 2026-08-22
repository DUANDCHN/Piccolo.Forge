using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 条带按钮角标（<see cref="ToolWindowViewModel.HasBadge"/>）：宿主翻 VM，按钮**即时**亮 / 灭，不等下一次同步。
/// 语义由宿主定（典型：通知面板的未读报警）；库只管画与刷新。
/// </summary>
public class StripeBadgeTests
{
    [AvaloniaFact]
    public void Badge_follows_the_view_model_immediately_without_a_sync()
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var frame = new ForgeControl();
        frame.Attach(shell);
        var window = new Window { Content = frame };
        window.Show();

        var vm = shell.Find("Project")!;
        var button = frame.GetVisualDescendants().OfType<ForgeStripeButton>().Single(b => ReferenceEquals(b.Tag, vm));
        Assert.False(button.HasBadge);
        Assert.DoesNotContain(":badge", button.Classes);

        vm.HasBadge = true;   // 没有任何 Open / Close / Sync
        Assert.True(button.HasBadge);
        Assert.Contains(":badge", button.Classes);

        vm.HasBadge = false;
        Assert.False(button.HasBadge);
    }

    [AvaloniaFact]
    public void Title_and_icon_changes_also_reach_the_button_immediately()
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var frame = new ForgeControl();
        frame.Attach(shell);
        var window = new Window { Content = frame };
        window.Show();

        var vm = shell.Find("Project")!;
        var button = frame.GetVisualDescendants().OfType<ForgeStripeButton>().Single(b => ReferenceEquals(b.Tag, vm));

        vm.Title = "Projekt";
        Assert.Equal("Projekt", button.Title);
    }
}
