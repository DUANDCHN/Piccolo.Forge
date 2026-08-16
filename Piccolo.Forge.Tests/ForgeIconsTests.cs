using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 库自绘的图标集。
public class ForgeIconsTests
{
    [AvaloniaTheory]
    [InlineData("Close")]
    [InlineData("Pin")]
    [InlineData("More")]
    [InlineData("SplitVertical")]
    [InlineData("SplitHorizontal")]
    [InlineData("DefaultToolWindow")]
    [InlineData("Project")]
    [InlineData("Structure")]
    [InlineData("Terminal")]
    [InlineData("VersionControl")]
    [InlineData("Commit")]
    [InlineData("Problems")]
    [InlineData("Services")]
    [InlineData("Notifications")]
    [InlineData("Database")]
    [InlineData("Find")]
    [InlineData("Run")]
    [InlineData("Debug")]
    [InlineData("Bookmarks")]
    [InlineData("TodoList")]
    [InlineData("Chart")]
    [InlineData("Camera")]
    [InlineData("Settings")]
    public void Every_icon_has_real_geometry(string name)
    {
        var icon = (Geometry)typeof(ForgeIcons).GetProperty(name)!.GetValue(null)!;

        Assert.True(icon.Bounds.Width > 0 && icon.Bounds.Height > 0);
    }

    [AvaloniaFact]
    public void Icons_stay_inside_the_sixteen_by_sixteen_box()
    {
        // 所有路径按 16×16 画，Draw 时等比缩放到目标尺寸
        Assert.Equal(16, ForgeIcons.ViewBoxSize);

        var all = typeof(ForgeIcons)
            .GetProperties()
            .Where(x => x.PropertyType == typeof(Geometry))
            .Select(x => (Geometry)x.GetValue(null)!)
            .ToList();

        Assert.True(all.Count >= 20, "图标集应当覆盖常见工具窗口");

        foreach (var icon in all)
        {
            Assert.True(icon.Bounds.Right <= ForgeIcons.ViewBoxSize, "超出画布右边");
            Assert.True(icon.Bounds.Bottom <= ForgeIcons.ViewBoxSize, "超出画布下边");
            Assert.True(icon.Bounds.X >= 0 && icon.Bounds.Y >= 0, "跑到画布外");
        }
    }

    [AvaloniaFact]
    public void A_stripe_button_takes_a_geometry_icon()
    {
        // Icon 是 object：Geometry 直接画，别的东西宿主自己渲染
        var button = new ForgeStripeButton { Icon = ForgeIcons.DefaultToolWindow };
        button.Measure(Size.Infinity);

        Assert.Equal(new Size(40, 40), button.DesiredSize);
        Assert.IsAssignableFrom<Geometry>(button.Icon);
    }

    [AvaloniaFact]
    public void The_shell_passes_the_panel_icon_through_to_the_button()
    {
        var shell = new ToolWindowShellViewModel();
        shell.Add("Project", "项目", ToolWindowAnchor.Left, icon: ForgeIcons.DefaultToolWindow);

        var frame = new ForgeToolWindowFrame();
        frame.Attach(shell);

        var button = Assert.Single(frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>());
        Assert.Same(ForgeIcons.DefaultToolWindow, button.Icon);
    }
}
