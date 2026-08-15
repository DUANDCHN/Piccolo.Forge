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
        Assert.Equal(5, button.IconPadding);
        Assert.Equal(12, button.Arc);

        button.IsCompact = true;
        Assert.Equal(16, button.IconSize);
        Assert.Equal(4, button.IconPadding);
        Assert.Equal(8, button.Arc);
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

        // 左内边距 8，竖直居中（标签页高 28、图标 16）
        Assert.Equal(new Rect(8, 6, 16, 16), icon.Bounds);
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

        Assert.Same(button, icon.GetVisualParent());

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

        button.Icon = second;

        Assert.Null(first.GetVisualParent());
        Assert.Same(button, second.GetVisualParent());
    }

    [AvaloniaFact]
    public void A_geometry_icon_hosts_no_control()
    {
        var button = new ForgeStripeButton { Icon = ForgeIcons.Terminal };

        button.Measure(Size.Infinity);
        button.Arrange(new Rect(button.DesiredSize));

        Assert.Empty(button.GetVisualChildren());
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
