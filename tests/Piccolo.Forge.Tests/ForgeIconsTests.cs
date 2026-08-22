using System;
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
    public void 标题条那两个图标的墨迹与图标包的同档()
    {
        // 由来：宿主在同一排里混了图标包的图标（字形几乎撑满 em 盒，16px 时墨迹 ≈13.3），
        // 上游那版 ⋮ 只有 10 高、— 只有 9 长，摆在一起明显小一圈。
        // ⚠ 大是靠改坐标拿到的：这两条路径按 16 网格的**整数**坐标画，1.0 倍画进 16×16
        //   的图标位时压在像素边界上；改成缩放（试过 1.15）会推到半像素、抗锯齿发虚。
        Assert.Equal(12, ForgeIcons.MoreVertical.Bounds.Height);
        Assert.Equal(12, ForgeIcons.Hide.Bounds.Width);

        // 整数坐标是"锐利"的前提，一并钉住
        foreach (var edge in new[]
                 {
                     ForgeIcons.MoreVertical.Bounds.X, ForgeIcons.MoreVertical.Bounds.Y,
                     ForgeIcons.MoreVertical.Bounds.Right, ForgeIcons.MoreVertical.Bounds.Bottom,
                     ForgeIcons.Hide.Bounds.X, ForgeIcons.Hide.Bounds.Right,
                 })
        {
            Assert.Equal(edge, Math.Round(edge));
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

        var frame = new ForgeControl();
        frame.Attach(shell);

        var button = Assert.Single(frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>());
        Assert.Same(ForgeIcons.DefaultToolWindow, button.Icon);
    }
}
