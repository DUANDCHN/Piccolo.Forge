using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
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
    /// <summary>
    /// **每一颗方按钮**在同一份外观下几何完全一致 —— 按反射遍历，不点名。
    /// </summary>
    /// <remarks>
    /// <b>防的是今天出过三次的那一类错</b>（2026-08-26）：条带按钮与「更多」按钮是兄弟，
    /// 度量本该同源，但每加一处几何都要记得喂两颗 —— 忘一处的症状全是
    /// "编得过、不报错、只是看着不对"（图标被撑大、按钮 40 而邻居 31、悬停底铺满）。
    /// <para>
    /// 判据取「同一份输入下算出来的东西相等」，并且**名单靠反射扫出来** ——
    /// 以后再多一颗方按钮，漏喂当场红，不必有人记得回来加用例。
    /// 这也是"两颗按钮的几何必须同源"这条约定唯一机器可查的形式。
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void 每颗方按钮的几何都同源()
    {
        var kinds = typeof(ForgeSquareButtonBase).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ForgeSquareButtonBase).IsAssignableFrom(t))
            .OrderBy(t => t.Name)
            .ToList();
        Assert.True(kinds.Count >= 2, "至少该有条带按钮与「更多」按钮两颗");

        var made = kinds.Select(t =>
        {
            var b = (ForgeSquareButtonBase)Activator.CreateInstance(t)!;
            b.IsLeftStripe = true;
            b.HighlightSize = 26;
            b.HighlightInsetHorizontal = 2.5;
            b.HighlightInsetVertical = 2.5;
            b.IconSizeOverride = 16;
            return (t.Name, Button: b);
        }).ToList();

        var (firstName, first) = made[0];
        var size = new Size(first.ButtonWidth, first.ButtonHeight);
        foreach (var (name, button) in made.Skip(1))
        {
            Assert.Equal(first.ButtonWidth, button.ButtonWidth);
            Assert.Equal(first.ButtonHeight, button.ButtonHeight);
            Assert.Equal(first.IconSize, button.IconSize);
            Assert.Equal(first.PlateInsets, button.PlateInsets);
            Assert.Equal(first.IconPlacement(size), button.IconPlacement(size));
            Assert.Equal(first.BackgroundRect(new Rect(size)), button.BackgroundRect(new Rect(size)));
        }

        _ = firstName;
    }

    /// <summary>它与工具窗口按钮**同尺寸、同图标大小**（宿主给了外观时也一样）。</summary>
    /// <remarks>
    /// <b>反面（2026-08-26 宿主实机："三个点歪了、背板大小也不一样"）。</b>两层原因叠着：
    /// <list type="number">
    /// <item>它没覆盖 <c>IconRect</c>，落到基类的「整块背板都是图标区」，图标被撑大一圈；</item>
    /// <item>更要命的是 <c>ForgeControl</c> 只给条带按钮喂 <c>Appearance</c> 里那套度量，
    /// 「更多」按钮一个都没拿到 —— 于是它 <c>IsHostSized</c> 为假，退回上游的 40×40 与
    /// Islands 的**左右不对称内边距**（5/2），比邻居大一圈、图标还偏一边。</item>
    /// </list>
    /// <para>
    /// ⚠ <b>所以这条必须走整机 + 宿主外观。</b>先前那版直接 new 两颗裸按钮比图标矩形，
    /// 两颗都没喂外观、自然相等 —— 功能坏成那样它照样绿。判据要落在
    /// <c>ForgeControl</c> 真正排出来的那两颗上。
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void 它与条带按钮同尺寸同图标()
    {
        var shell = new ToolWindowShellViewModel();
        var hidden = shell.Add("Hidden", "藏起来的", ToolWindowAnchor.Left, false);
        shell.Add("Shown", "露着的", ToolWindowAnchor.Left, false);
        ToolWindowContextMenu.Hide(shell.Manager, hidden.Id);

        var frame = new ForgeControl();
        frame.Attach(shell);
        // 宿主收窄条带（LDVision 就是这么用的）—— 正是这一档下两颗按钮会长得不一样
        frame.Appearance = ForgeAppearance.Default with
        {
            StripeHighlightSize = 26,
            StripeHighlightInsetHorizontal = 2.5,
            StripeHighlightInsetVertical = 2.5,
            StripeIconSize = 16,
        };
        var window = new Window { Width = 900, Height = 600, Content = frame };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var more = frame.GetVisualDescendants().OfType<ForgeMoreButton>().First(x => x.IsAvailable);
        var stripe = frame.GetVisualDescendants().OfType<ForgeStripeButton>().First();

        Assert.Equal(stripe.ButtonWidth, more.ButtonWidth);
        Assert.Equal(stripe.ButtonHeight, more.ButtonHeight);
        Assert.Equal(stripe.IconSize, more.IconSize);

        var size = new Size(stripe.ButtonWidth, stripe.ButtonHeight);
        Assert.Equal(stripe.IconPlacement(size), more.IconPlacement(size));

        // ⚠ 还要比**画出来的背板**（悬停底）：上面那几个属性对得上，模板里少一条
        //   :square-background 样式，底照样铺满整颗按钮 —— 实机就是"三个点的悬停底大一圈"。
        //   判据只能落在渲染出来的 Bounds 上。
        Assert.Equal(Plate(stripe).Bounds.Size, Plate(more).Bounds.Size);

        window.Close();

        static Border Plate(Control button)
            => button.GetVisualDescendants().OfType<Border>().First(x => x.Name == "PART_Background");
    }

    // ⚠ 必须 AvaloniaTheory：new ForgeMoreButton() 会走到 ForgeIcons（取几何图形需要渲染接口），
    //   裸 [Theory] 没有 Avalonia 会话，跑到的顺序不同就抛 TypeInitializationException ——
    //   全量跑时**时绿时红**（2026-08-26 撞到）。
    [AvaloniaTheory]
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

    /// <summary>
    /// 隐藏首块面板会把 <see cref="ForgeMoreButton.HasHiddenToolWindows"/> 从 false 改成 true；
    /// 入口必须在本轮布局中取得尺寸，不能等窗口最大化等外部尺寸变化来补一次测量。
    /// </summary>
    [AvaloniaFact]
    public void Becoming_available_invalidates_measure_and_appears_without_window_resize()
    {
        var button = new ForgeMoreButton
        {
            Side = StripeSide.Left,
            ActiveSide = StripeSide.Left,
            HasHiddenToolWindows = false,
        };
        var window = new Window { Width = 200, Height = 200, Content = button };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(default, button.DesiredSize);

        button.HasHiddenToolWindows = true;
        Dispatcher.UIThread.RunJobs();

        Assert.True(button.DesiredSize.Width > 0);
        Assert.True(button.DesiredSize.Height > 0);
        Assert.True(button.Bounds.Width > 0);
        Assert.True(button.Bounds.Height > 0);
        window.Close();
    }

    // ⚠ 必须 AvaloniaTheory：new ForgeMoreButton() 会走到 ForgeIcons（取几何图形需要渲染接口），
    //   裸 [Theory] 没有 Avalonia 会话，跑到的顺序不同就抛 TypeInitializationException ——
    //   全量跑时**时绿时红**（2026-08-26 撞到）。
    [AvaloniaTheory]
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
        var frame = new ForgeControl();
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
