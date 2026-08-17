using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 控件的度量与状态色。
public class ForgeControlsTests
{
    [AvaloniaFact]
    public void Stripe_button_is_40_by_40_and_32_when_compact()
    {
        // JBUI.java:1322-1324 与 darcula.theme.json 的 StripeToolbar.Button.size.compact
        var button = new ForgeStripeButton();
        button.Measure(Size.Infinity);
        Assert.Equal(new Size(40, 40), button.DesiredSize);

        button.IsCompact = true;
        button.InvalidateMeasure();
        button.Measure(Size.Infinity);
        Assert.Equal(new Size(32, 32), button.DesiredSize);
    }

    [AvaloniaFact]
    public void Stripe_button_metrics_follow_density()
    {
        var button = new ForgeStripeButton();
        Assert.Equal(20, button.IconSize);
        Assert.Equal(new ForgeInsets(5), button.IconPadding);
        Assert.Equal(12, button.Arc);

        button.IsCompact = true;
        Assert.Equal(16, button.IconSize);
        Assert.Equal(new ForgeInsets(4), button.IconPadding);
        Assert.Equal(8, button.Arc);
    }

    /// <summary>
    /// Islands 把条带按钮收窄到 37×40，且左右内边距镜像不对称——
    /// 靠窗口内侧留 2、外侧留 5（<c>docs/upstream/Islands主题.md</c> §4）。
    /// </summary>
    [AvaloniaFact]
    public void Islands_narrows_the_stripe_button_and_mirrors_its_icon_padding()
    {
        var button = new ForgeStripeButton { Palette = ForgePalette.IslandsDark };

        button.Measure(Size.Infinity);
        Assert.Equal(new Size(37, 40), button.DesiredSize);

        // 左条带：右边（朝窗口里）留 2
        Assert.Equal(new ForgeInsets(5, 5, 5, 2), button.IconPadding);

        // 右条带：镜像过来，左边留 2
        button.IsLeftStripe = false;
        Assert.Equal(new ForgeInsets(5, 2, 5, 5), button.IconPadding);

        // expUI 仍是 40×40、四边相等 —— 换族不该改到它
        button.Palette = ForgePalette.ExpUiDark;
        button.InvalidateMeasure();
        button.Measure(Size.Infinity);
        Assert.Equal(new Size(40, 40), button.DesiredSize);
        Assert.Equal(new ForgeInsets(5), button.IconPadding);
    }

    /// <summary>
    /// 两套主题的高亮矩形都是 30×30（<c>40-5-5</c> 与 <c>37-5-2</c>），
    /// Islands 只是把它整体推向条带外缘 —— 回归：早先这里画的是整个按钮矩形。
    /// </summary>
    [AvaloniaTheory]
    [InlineData(false, 5d, 30d)]
    [InlineData(true, 5d, 30d)]
    public void The_highlight_is_the_button_minus_its_icon_padding(bool islands, double expectedLeft, double expectedSize)
    {
        var button = new ForgeStripeButton
        {
            Palette = islands ? ForgePalette.IslandsDark : ForgePalette.ExpUiDark,
        };

        button.Measure(Size.Infinity);
        var rect = button.BackgroundRect(new Rect(button.DesiredSize));

        Assert.Equal(expectedLeft, rect.X);
        Assert.Equal(expectedSize, rect.Width);
        Assert.Equal(expectedSize, rect.Height);
    }

    /// <summary>
    /// 图标与高亮**共用一个矩形**（都是「按钮 − 图标内边距」），所以图标在高亮里居中，
    /// 而不是在整个按钮里居中。
    /// </summary>
    /// <remarks>
    /// 出处 <c>SquareStripeButtonLook.kt:112-118 · getIconPosition</c> —— 它与
    /// <c>paintBackground</c> 扣的是同一份 <c>iconPadding</c>。
    /// <para>
    /// 回归：本来按整个按钮宽居中。expUI 内边距左右对称、两种算法重合，看不出错；
    /// Islands 是 5/2 不对称，图标就比高亮偏 1.5px（实机截图里"图标偏了"）。
    /// </para>
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(true, 10d)]   // 左条带：padding 5/2 → 高亮 x∈[5,35]，中心 20，图标 20 宽 → x=10
    [InlineData(false, 7d)]   // 右条带：镜像，padding 2/5 → 高亮 x∈[2,32]，中心 17 → x=7
    public void The_icon_is_centred_on_the_highlight_not_on_the_button(bool isLeft, double expectedIconX)
    {
        var button = new ForgeStripeButton
        {
            Palette = ForgePalette.IslandsDark,
            IsLeftStripe = isLeft,
        };

        button.Measure(Size.Infinity);
        var cell = new Rect(button.DesiredSize);

        var highlight = button.BackgroundRect(cell);
        var iconX = highlight.X + ((highlight.Width - button.IconSize) / 2);

        Assert.Equal(expectedIconX, iconX);

        // 按整个按钮居中会得到 (37-20)/2 = 8.5 —— 两者必须不同，否则这条用例没有判别力
        Assert.NotEqual((cell.Width - button.IconSize) / 2, iconX);
    }

    /// <summary>
    /// 紧凑 + 显名时右条带的内边距是 <b>4</b> 而不是 5：
    /// <c>rightStripeIconWithName.padding.compact</c> 在上游不存在，
    /// 而紧凑是「有 <c>.compact</c> 才覆盖」，于是保留基值 4。
    /// </summary>
    [AvaloniaFact]
    public void A_missing_compact_key_keeps_the_base_value_rather_than_the_code_fallback()
    {
        var metrics = ForgeStripeButtonMetrics.ForTheme(ForgeThemeFamily.ExpUi, compact: true);

        Assert.Equal(new ForgeInsets(3), metrics.IconPadding(isLeft: true, showName: true));
        Assert.Equal(new ForgeInsets(4), metrics.IconPadding(isLeft: false, showName: true));
    }

    [AvaloniaFact]
    public void Visible_beats_hover_in_the_background_priority()
    {
        // SquareStripeButton.kt:208-219 —— 焦点 > 窗口可见 > 悬停 > 无
        var button = new ForgeStripeButton { Palette = ForgePalette.ExpUiDark };

        Assert.Null(button.ResolveBackground());

        button.IsToolWindowVisible = true;
        Assert.Equal(ForgePalette.ExpUiDark.ButtonPressedBackground, button.ResolveBackground());

        button.IsToolWindowActive = true;
        Assert.Equal(ForgePalette.ExpUiDark.ButtonSelectedBackground, button.ResolveBackground());
    }

    [AvaloniaFact]
    public void Active_button_uses_the_selected_foreground()
    {
        var button = new ForgeStripeButton { Palette = ForgePalette.ExpUiLight };
        Assert.Equal(ForgePalette.ExpUiLight.ButtonForeground, button.ResolveForeground());

        button.IsToolWindowActive = true;
        Assert.Equal(ForgePalette.ExpUiLight.ButtonSelectedForeground, button.ResolveForeground());
    }

    [AvaloniaFact]
    public void Separator_reserves_32_by_11()
    {
        // StripeButtonSeparator.kt:22-24
        var separator = new ForgeStripeSeparator();
        separator.Measure(Size.Infinity);
        Assert.Equal(new Size(32, 11), separator.DesiredSize);
    }

    [AvaloniaFact]
    public void Toolbar_is_button_width_plus_one_pixel_border()
    {
        // ToolWindowLeftToolbar.kt:28 —— 右缘一条 1px 线
        var toolbar = new ForgeToolWindowToolbar { Side = StripeSide.Left };
        toolbar.TopGroup.Children.Add(new ForgeStripeButton());

        toolbar.Measure(new Size(double.PositiveInfinity, 600));

        Assert.Equal(41, toolbar.DesiredSize.Width);
        Assert.True(toolbar.HasButtons);
    }

    [AvaloniaFact]
    public void Empty_toolbar_reports_no_buttons()
    {
        // 没有按钮的条带整根隐藏（ToolWindowPaneNewButtonManager.kt:76-99）
        Assert.False(new ForgeToolWindowToolbar().HasButtons);
    }

    [AvaloniaFact]
    public void Header_is_41_high_and_31_when_compact()
    {
        // JBUI.java:1178-1180 与 ToolWindow.Header.height.compact
        var header = new ForgeToolWindowHeader { Title = "Project" };
        header.Measure(new Size(300, double.PositiveInfinity));
        Assert.Equal(41, header.DesiredSize.Height);

        header.IsCompact = true;
        header.Measure(new Size(300, double.PositiveInfinity));
        Assert.Equal(31, header.DesiredSize.Height);
    }

    [AvaloniaFact]
    public void An_editor_tab_leaves_room_for_the_file_icon()
    {
        // EditorTabbedContainer.kt:612 · iconTextGap = JBUI.scale(4)，图标 16×16。
        var bare = new ForgeEditorTab { Title = "cam-1.png" };
        bare.Measure(Size.Infinity);

        var withIcon = new ForgeEditorTab { Title = "cam-1.png", Icon = ForgeIcons.Camera };
        withIcon.Measure(Size.Infinity);

        Assert.Equal(bare.DesiredSize.Width + 16 + 4, withIcon.DesiredSize.Width);
    }

    [AvaloniaFact]
    public void An_editor_tab_hosts_a_control_icon_on_the_left()
    {
        var icon = new Border();
        var tab = new ForgeEditorTab { Title = "cam-1.png", Icon = icon };

        tab.Measure(Size.Infinity);
        tab.Arrange(new Rect(tab.DesiredSize));

        Assert.Same(tab, icon.GetVisualParent());

        // 左内边距 8，竖直居中。
        // ⚠ 行高**不是** 28：EditorTabbedContainer.kt:784-788 算的是
        //    UNSCALED_PREF_HEIGHT(28) - tabInsets.top - tabInsets.bottom，
        //    而 tabInsets 上下是负值（expUI -7）→ 42。
        //    早先按 28 写死，于是这条用例把错的行高一起钉住了。
        // ⚠ 左内边距**不是** 8：标签页有两层（TabLabel.kt:583-587）——
        //    labelInsets.left(8) + contentInsets.left(4) = 12。早先只算了外层，看着偏窄。
        var rowHeight = ForgeMetrics.EditorTabRowHeight(islands: false);
        Assert.Equal(42, rowHeight);
        Assert.Equal(12, ForgeMetrics.EditorTabTextInsetLeft);
        Assert.Equal(
            new Rect(ForgeMetrics.EditorTabTextInsetLeft, (rowHeight - 16) / 2, 16, 16),
            icon.Bounds);
    }

    /// <summary>
    /// 宿主可以指定文档标签的行高（上游 Islands 40 / expUI 42），Islands 那个圆角块跟着自适应。
    /// </summary>
    [AvaloniaFact]
    public void 宿主指定的标签行高盖过上游默认()
    {
        var tab = new ForgeEditorTab { Title = "cam-1.png", RowHeightOverride = 31 };
        tab.Measure(Size.Infinity);

        Assert.Equal(31, tab.DesiredSize.Height);

        // 不设就还是上游那两档
        var upstream = new ForgeEditorTab { Title = "cam-1.png" };
        upstream.Measure(Size.Infinity);
        Assert.Equal(ForgeMetrics.EditorTabRowHeight(islands: false), upstream.DesiredSize.Height);
    }

    [AvaloniaFact]
    public void The_icon_is_20_square_and_centred_not_button_minus_padding()
    {
        // 回归：第一版按 Bounds - IconPadding × 2 算，得 30×30，图标被放大到 1.5 倍。
        // 上游 SquareStripeButton.kt:356-367 · scaleIcon 是把图标**缩放成 iconSize**（20），
        // padding 决定的是按钮内容区，不是图标大小。
        var icon = new Border();
        var button = new ForgeStripeButton { Icon = icon };

        button.Measure(Size.Infinity);
        button.Arrange(new Rect(button.DesiredSize));

        Assert.Equal(new Rect(10, 10, 20, 20), icon.Bounds);
    }

    [AvaloniaFact]
    public void The_icon_shrinks_with_density()
    {
        // 紧凑：按钮 32、图标 16 → 居中在 (8, 8)
        var icon = new Border();
        var button = new ForgeStripeButton { Icon = icon, IsCompact = true };

        button.Measure(Size.Infinity);
        button.Arrange(new Rect(button.DesiredSize));

        Assert.Equal(new Rect(8, 8, 16, 16), icon.Bounds);
    }

    [AvaloniaFact]
    public void The_resizer_can_actually_be_grabbed()
    {
        // 回归：Avalonia 的命中测试基于渲染内容 —— 什么都不画的 Control 收不到指针事件，
        // 于是热区在那儿、光标也变了，就是拖不动。
        var resizer = new ForgeDockResizer { Anchor = ToolWindowAnchor.Left };
        var host = new Border { Child = resizer };
        var window = new Window { Width = 200, Height = 200, Content = host };

        DockWeightChangedEventArgs? changed = null;
        resizer.WeightChanged += (_, e) => changed = e;

        window.Show();
        Dispatcher.UIThread.RunJobs();

        // 走完整输入管线：按下 → 拖 → 抬起。收不到事件就说明命中测试没把它算进去。
        window.MouseDown(new Point(100, 100), MouseButton.Left);
        window.MouseMove(new Point(60, 100));
        window.MouseUp(new Point(60, 100), MouseButton.Left);

        Assert.NotNull(changed);
        Assert.Equal(ToolWindowAnchor.Left, changed!.Anchor);
    }

    // ── 宿主塞控件当图标（Icon 是 object，允许 Geometry / Control / null）────

    [AvaloniaFact]
    public void A_host_control_icon_lands_in_the_visual_tree()
    {
        // 回归：Icon 声明成 object 且文档承诺「塞控件也行」，但渲染只认 Geometry ——
        // 宿主给 MaterialIcon 这类控件时，既不画它、也不画默认图形，按钮一片空白。
        var icon = new Border { Width = 20, Height = 20 };
        var button = new ForgeStripeButton { Icon = icon };

        button.Measure(Size.Infinity);
        button.Arrange(new Rect(button.DesiredSize));

        // 图标挂在**模板里的槽**（ForgeIconHost）上，不直接挂按钮 —— Button 的 ApplyTemplate
        // 会清空 VisualChildren，手工挂在按钮身上的东西活不过一次测量（见 ForgeIconHost）。
        Assert.Contains(icon, button.GetVisualDescendants());

        // 图标位是 40 - padding 5 × 2 = 30×30，居中放在按钮里；
        // 控件自己有多大由它自己说了算（这里 Border 显式 20×20），只要它是居中的。
        Assert.Equal(new Point(20, 20), icon.Bounds.Center);
        Assert.True(icon.Bounds.Width <= 30 && icon.Bounds.Height <= 30);
    }

    [AvaloniaFact]
    public void Swapping_the_icon_control_detaches_the_old_one()
    {
        var first = new Border();
        var second = new Border();
        var button = new ForgeStripeButton { Icon = first };

        button.Measure(Size.Infinity);
        button.Arrange(new Rect(button.DesiredSize));
        button.Icon = second;
        button.Measure(Size.Infinity);
        button.Arrange(new Rect(button.DesiredSize));

        Assert.DoesNotContain(first, button.GetVisualDescendants());
        Assert.Contains(second, button.GetVisualDescendants());
    }

    [AvaloniaFact]
    public void A_geometry_icon_hosts_no_control()
    {
        var button = new ForgeStripeButton { Icon = ForgeIcons.Terminal };

        button.Measure(Size.Infinity);
        button.Arrange(new Rect(button.DesiredSize));

        // 模板里那个槽恒在（Button 必须有模板），槽里没有图标控件才是「不托管控件」
        Assert.DoesNotContain(button.GetVisualDescendants(), x => x is Border);
    }

    [AvaloniaFact]
    public void A_host_control_icon_follows_the_active_state_colour()
    {
        // 不跟着走的话，浅色主题下是一排看不见的白图标 —— 而库自绘那套是好的，
        // 很难猜到原因在这里。
        var button = new ForgeStripeButton
        {
            Icon = new Border(),
            Palette = ForgePalette.ExpUiLight,
        };

        Assert.Equal(ForgePalette.ExpUiLight.ButtonForeground, ((SolidColorBrush)button.Foreground!).Color);

        button.IsToolWindowActive = true;
        Assert.Equal(ForgePalette.ExpUiLight.ButtonSelectedForeground, ((SolidColorBrush)button.Foreground!).Color);
    }
}

/// 颜色字面量的位序。
public class ForgeColorTests
{
    [Fact]
    public void Six_digit_hex_is_opaque_rgb()
    {
        var color = ForgeColor.Parse("#3574F0");
        Assert.Equal(255, color.A);
        Assert.Equal(0x35, color.R);
        Assert.Equal(0x74, color.G);
        Assert.Equal(0xF0, color.B);
    }

    [Fact]
    public void Eight_digit_hex_puts_alpha_last()
    {
        // 上游是 #RRGGBBAA，与 Avalonia 的 #AARRGGBB 相反。
        // 证据：ToolWindow.DragAndDrop.areaBackground = #A0BDF84D，兜底是 JBColor(0xA0BDF8, ...)
        var color = ForgeColor.Parse("#A0BDF84D");
        Assert.Equal(0xA0, color.R);
        Assert.Equal(0xBD, color.G);
        Assert.Equal(0xF8, color.B);
        Assert.Equal(0x4D, color.A);
    }

    [Fact]
    public void Action_button_hover_is_a_translucent_overlay()
    {
        // ActionButton.hoverBackground = #00000012（light）/ #FFFFFF16（dark）
        Assert.Equal(0x12, ForgePalette.ExpUiLight.ButtonHoverBackground.A);
        Assert.Equal(0, ForgePalette.ExpUiLight.ButtonHoverBackground.R);
        Assert.Equal(0x16, ForgePalette.ExpUiDark.ButtonHoverBackground.A);
        Assert.Equal(255, ForgePalette.ExpUiDark.ButtonHoverBackground.R);
    }

    [Fact]
    public void Bad_length_is_rejected()
        => Assert.Throws<FormatException>(() => ForgeColor.Parse("#ABC"));
}
