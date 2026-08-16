using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 「更多」按钮。
public class ForgeMoreButtonTests
{
    [Theory]
    // MoreSquareStripeButton.kt:77-79 —— 边要对得上，且有东西可挑
    [InlineData(StripeSide.Left, StripeSide.Left, true, true)]
    [InlineData(StripeSide.Right, StripeSide.Left, true, false)]
    [InlineData(StripeSide.Left, StripeSide.Left, false, false)]
    public void Only_one_side_shows_it(StripeSide side, StripeSide active, bool hasHidden, bool expected)
    {
        var button = new ForgeMoreButton { Side = side, ActiveSide = active, HasHiddenToolWindows = hasHidden };
        Assert.Equal(expected, button.IsAvailable);
    }

    [AvaloniaFact]
    public void An_unavailable_more_button_takes_no_space()
    {
        var button = new ForgeMoreButton { Side = StripeSide.Right, ActiveSide = StripeSide.Left };
        button.Measure(Size.Infinity);

        Assert.Equal(default, button.DesiredSize);
    }

    [Theory]
    // MoreSquareStripeButton.kt:118-121
    [InlineData(StripeSide.Left, 41d, 41d)]
    [InlineData(StripeSide.Right, 41d, -300d)]
    public void The_popup_opens_towards_the_editor(StripeSide side, double stripeWidth, double expectedX)
        => Assert.Equal(expectedX, ForgeMoreButton.ComputePopupOffset(side, stripeWidth, 0).X);

    [AvaloniaFact]
    public void The_frame_puts_it_on_the_left_by_default()
    {
        // ToolWindowManagerState.kt:60 —— 默认 LEFT
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var manager = shell.Manager;
        manager.Hide("Project", removeFromStripe: true);   // 造一个「被摘掉的」
        var frame = new ForgeToolWindowFrame();
        frame.Attach(shell);

        var left = frame.LeftToolbar.TopGroup.Children.OfType<ForgeMoreButton>().Single();
        var right = frame.RightToolbar.TopGroup.Children.OfType<ForgeMoreButton>().Single();

        Assert.Equal(StripeSide.Left, frame.MoreButtonSide);
        Assert.False(right.IsAvailable);
        Assert.Equal(left.Side, left.ActiveSide);
    }
}

/// tab 上的关闭 / 固定。
public class TabIconTests
{
    [Fact]
    public void A_pinned_tab_unpins_instead_of_closing()
    {
        // ContentTabLabel.java:250-254
        var contents = new ToolWindowContentManager();
        contents.Add(new ToolWindowContent("a", "A", IsPinned: true));

        Assert.Equal(TabIconAction.Unpin, contents.InvokeIconAction("a"));
        Assert.Single(contents.Contents);
        Assert.False(contents.Contents[0].IsPinned);

        // 解完固定再点才是关
        Assert.Equal(TabIconAction.Close, contents.InvokeIconAction("a"));
        Assert.Empty(contents.Contents);
    }

    [Fact]
    public void Alt_click_closes_every_other_tab()
    {
        // ContentTabLabel.java:255-257
        var contents = new ToolWindowContentManager();
        contents.Add(new ToolWindowContent("a", "A"));
        contents.Add(new ToolWindowContent("b", "B"));
        contents.Add(new ToolWindowContent("c", "C"));

        Assert.Equal(TabIconAction.CloseOthers, contents.InvokeIconAction("b", isAltPressed: true));

        Assert.Single(contents.Contents);
        Assert.Equal("b", contents.Contents[0].Id);
    }

    [Fact]
    public void Non_closeable_content_has_no_icon()
    {
        // ContentTabLabel.java:169-171 —— 内容可关 且 工具窗口允许关内容
        var contents = new ToolWindowContentManager();
        contents.Add(new ToolWindowContent("a", "A", IsCloseable: false));

        Assert.Equal(TabIconAction.None, contents.ResolveIconAction("a"));
    }

    [Fact]
    public void A_window_that_forbids_closing_wins_over_the_content_flag()
    {
        var contents = new ToolWindowContentManager { CanCloseContents = false };
        contents.Add(new ToolWindowContent("a", "A"));

        Assert.False(contents.CanBeClosed(contents.Contents[0]));
        Assert.Equal(TabIconAction.None, contents.ResolveIconAction("a"));
    }

    [AvaloniaFact]
    public void The_icon_widens_the_tab()
    {
        // ContentLabel.java:252-257 —— 图标 + 间距 3 + 右缘 4
        var plain = new ForgeContentTab { Title = "Shelf" };
        var closeable = new ForgeContentTab { Title = "Shelf", CanBeClosed = true };

        plain.Measure(new Size(double.PositiveInfinity, 41));
        closeable.Measure(new Size(double.PositiveInfinity, 41));

        Assert.Equal(0, plain.IconAreaWidth);
        Assert.True(closeable.IconAreaWidth > ForgeMetrics.TabIconsGap + ForgeMetrics.TabRightIconInset);
        Assert.True(closeable.DesiredSize.Width > plain.DesiredSize.Width);
    }
}

/// 右键菜单。
public class ContextMenuTests
{
    [Fact]
    public void The_stripe_button_menu_has_three_sections()
    {
        // SquareStripeButton.kt:381-390
        var items = ToolWindowContextMenu.ForStripeButton();

        Assert.Equal(ToolWindowMenuItemKind.Hide, items[0].Kind);
        Assert.Equal(ToolWindowMenuItemKind.ToggleShowNames, items[^1].Kind);
        Assert.Equal(2, items.Count(x => x.Kind == ToolWindowMenuItemKind.Separator));
        Assert.Equal(6, items.Count(x => x.Kind == ToolWindowMenuItemKind.MoveTo));
    }

    [Fact]
    public void Move_to_covers_both_halves_of_every_reachable_anchor()
    {
        // 新 UI 落不了 TOP（defaultToolWindowlayoutProvider.kt:234）
        var moves = ToolWindowContextMenu.ForStripeButton()
            .Where(x => x.Kind == ToolWindowMenuItemKind.MoveTo)
            .ToList();

        Assert.DoesNotContain(moves, x => x.Anchor == ToolWindowAnchor.Top);
        Assert.All(
            new[] { ToolWindowAnchor.Left, ToolWindowAnchor.Right, ToolWindowAnchor.Bottom },
            anchor => Assert.Equal(2, moves.Count(x => x.Anchor == anchor)));
    }

    [Fact]
    public void The_background_menu_only_toggles_names()
    {
        // ResizeStripeManager.kt:51-59
        var item = Assert.Single(ToolWindowContextMenu.ForStripeBackground());
        Assert.Equal(ToolWindowMenuItemKind.ToggleShowNames, item.Kind);
    }

    [Fact]
    public void Hiding_an_already_hidden_window_still_removes_its_stripe_button()
    {
        // ToolWindowManagerImpl.kt:819-854 对「本来就不可见」没有早返回，
        // 摘条带按钮那步照做 —— 「更多」弹窗里能挑到的就是这批。
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());
        Assert.False(manager.Layout.GetInfo("Maven")!.IsVisible);

        ToolWindowContextMenu.Hide(manager, "Maven");

        Assert.False(manager.Layout.GetInfo("Maven")!.IsShowStripeButton);
    }

    [Fact]
    public void Hiding_an_invisible_window_without_removing_the_button_is_a_no_op()
    {
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());

        Assert.False(manager.Hide("Maven"));
        Assert.True(manager.Layout.GetInfo("Maven")!.IsShowStripeButton);
    }

    [Fact]
    public void Hide_also_removes_the_stripe_button()
    {
        // SquareStripeButton.kt:380-388 —— removeFromStripe = true
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());
        manager.Show("Project");

        ToolWindowContextMenu.Hide(manager, "Project");

        var info = manager.Layout.GetInfo("Project")!;
        Assert.False(info.IsVisible);
        Assert.False(info.IsShowStripeButton);
    }
}

/// 浮动窗口的淡出。
public class FloatingFadeTests
{
    [Fact]
    public void It_is_off_by_default()
        => Assert.False(ForgeMetrics.FloatingFadeEnabledByDefault);

    [Fact]
    public void Constants_match_upstream()
    {
        // FloatingDecorator.java:73-74 + UISettingsState.kt:182,184
        Assert.Equal(15, ForgeMetrics.FloatingFadeFrameDelayMs);
        Assert.Equal(7, ForgeMetrics.FloatingFadeTotalFrames);
        Assert.Equal(1500, ForgeMetrics.FloatingFadeDelayMs);
        Assert.Equal(0.5f, ForgeMetrics.FloatingFadeRatio);
    }

    [Fact]
    public void Fading_out_is_linear_across_the_frames()
    {
        var fade = FloatingFade.OnDeactivated(0f, 0.5f, 7);

        Assert.Equal(0f, fade.RatioAt(0), 5);
        Assert.Equal(0.5f, fade.RatioAt(7), 5);
        Assert.Equal(0.25f, fade.RatioAt(3.5f is var _ ? 3 : 3), 1);
    }

    [Fact]
    public void Fading_back_in_runs_twice_as_fast()
    {
        // FloatingDecorator.java:311-318 —— startRatio > endRatio 时 delta *= 2
        var fade = FloatingFade.OnActivated(0.5f, 7);

        // 一半的帧数就走完全程
        Assert.Equal(0f, fade.RatioAt(4), 5);
        Assert.True(fade.RatioAt(2) < 0.25f);
    }

    [Fact]
    public void Ratios_stay_inside_the_unit_range()
    {
        var fade = FloatingFade.OnActivated(0.5f, 7);
        Assert.Equal(0f, fade.RatioAt(100), 5);
    }

    [Theory]
    // FloatingDecorator.java:291-293 —— 打断重来时帧号要翻过来
    [InlineData(0, 0)]
    [InlineData(2, 5)]
    [InlineData(7, 0)]
    public void An_interrupted_animation_flips_the_frame(int frame, int expected)
        => Assert.Equal(expected, FloatingFade.FlipFrame(frame, 7));
}
