using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 右键菜单接线。
public class ForgeMenuBuilderTests
{
    [Theory]
    // 名字取自位置图标：竖边的另一半在下，横边的另一半在右
    // （ToolWindowManagerImpl.kt:770-786 · getLocationIcon）
    [InlineData(ToolWindowAnchor.Left, false, "左上")]
    [InlineData(ToolWindowAnchor.Left, true, "左下")]
    [InlineData(ToolWindowAnchor.Bottom, false, "下左")]
    [InlineData(ToolWindowAnchor.Bottom, true, "下右")]
    public void Move_items_are_named_after_the_eight_slots(ToolWindowAnchor anchor, bool split, string expected)
        => Assert.Equal(expected, ForgeMenuBuilder.DescribeMove(anchor, split));

    [AvaloniaFact]
    public void The_stripe_button_menu_maps_one_to_one_onto_the_model()
    {
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());
        var menu = ForgeMenuBuilder.ForStripeButton(manager, "Project", () => { });

        Assert.Equal(ToolWindowContextMenu.ForStripeButton().Count, menu.Items.Count);
        Assert.Equal(2, menu.Items.OfType<Separator>().Count());
    }

    [AvaloniaFact]
    public void The_background_menu_only_has_the_names_toggle()
    {
        var menu = ForgeMenuBuilder.ForStripeBackground(() => { });
        var item = Assert.Single(menu.Items.OfType<MenuItem>());

        Assert.Equal("显示工具窗口名字", item.Header);
    }

    [AvaloniaFact]
    public void The_frame_hangs_a_menu_on_every_stripe_button()
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var manager = shell.Manager;
        var frame = new ForgeToolWindowFrame();
        frame.Attach(shell);

        Assert.All(
            frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>(),
            x => Assert.NotNull(x.ContextMenu));
        Assert.NotNull(frame.LeftToolbar.ContextMenu);
    }
}

/// 「更多」弹窗。
public class ForgeMorePopupTests
{
    [Fact]
    public void It_lists_the_windows_whose_stripe_button_was_removed()
    {
        // 就是被 removeFromStripe 摘掉的那批（SquareStripeButton.kt:380-388）
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());
        Assert.Empty(ForgeMorePopup.ResolveItems(manager.Layout));

        ToolWindowContextMenu.Hide(manager, "Maven");

        Assert.Equal(["Maven"], ForgeMorePopup.ResolveItems(manager.Layout));
    }

    [Fact]
    public void A_visible_window_never_shows_up_there()
    {
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());
        manager.Activate("Project");

        Assert.DoesNotContain("Project", ForgeMorePopup.ResolveItems(manager.Layout));
    }

    [AvaloniaFact]
    public void Nothing_to_show_means_no_popup()
        => Assert.Null(ForgeMorePopup.Create(new ToolWindowManager(DefaultToolWindowLayout.CreateV2())));

    [AvaloniaFact]
    public void The_popup_is_at_least_300_wide()
    {
        // MoreSquareStripeButton.kt:106
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());
        ToolWindowContextMenu.Hide(manager, "Maven");

        var flyout = ForgeMorePopup.Create(manager);

        Assert.NotNull(flyout);
        Assert.Equal(ForgeMetrics.MorePopupMinWidth, ((StackPanel)flyout.Content!).MinWidth);
    }
}

/// 浮动窗口淡出的定时器。
public class ForgeFadeControllerTests
{
    [AvaloniaFact]
    public void It_starts_fully_opaque_and_idle()
    {
        using var fade = new ForgeFadeController(new Window());

        Assert.Equal(0f, fade.CurrentRatio);
        Assert.False(fade.IsRunning);
    }

    [AvaloniaFact]
    public void Activating_starts_the_animation_right_away()
    {
        // FloatingDecorator.java:289-296 —— 激活分支没有延迟
        using var fade = new ForgeFadeController(new Window());

        fade.OnActivated();

        Assert.True(fade.IsRunning);
    }

    [AvaloniaFact]
    public void Deactivating_waits_before_it_starts()
    {
        // :298-310 —— 先等 alphaModeDelay(1500ms)
        using var fade = new ForgeFadeController(new Window());

        fade.OnDeactivated();

        Assert.False(fade.IsRunning);
    }

    [AvaloniaFact]
    public void The_target_ratio_defaults_to_the_upstream_value()
        => Assert.Equal(ForgeMetrics.FloatingFadeRatio, new ForgeFadeController(new Window()).TargetRatio);
}
