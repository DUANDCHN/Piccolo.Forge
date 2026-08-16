using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 条带的「显示名字」模式。
public class ForgeShowNamesTests
{
    [Theory]
    // SquareStripeButton.kt:331-334 —— split(" ", limit = 2)
    [InlineData("Project", new[] { "Project" })]
    [InlineData("Version Control", new[] { "Version", "Control" })]
    [InlineData("Problems View Extra", new[] { "Problems", "View Extra" })]
    [InlineData("  Terminal  ", new[] { "Terminal" })]
    [InlineData("", new string[0])]
    public void The_title_splits_on_the_first_space_into_at_most_two_lines(string title, string[] expected)
        => Assert.Equal(expected, ForgeStripeButton.SplitTitle(title));

    [AvaloniaFact]
    public void Showing_names_makes_the_button_taller_and_as_wide_as_the_stripe()
    {
        var plain = new ForgeStripeButton { Title = "Project" };
        plain.Measure(Size.Infinity);
        Assert.Equal(new Size(40, 40), plain.DesiredSize);

        var named = new ForgeStripeButton
        {
            Title = "Project",
            ShowName = true,
            StripeWidth = ForgeMetrics.StripeNamedDefaultWidth,
        };
        named.Measure(Size.Infinity);

        Assert.Equal(59, named.DesiredSize.Width);
        Assert.True(named.DesiredSize.Height > 40, "显示名字时按钮要变高");
    }

    [AvaloniaFact]
    public void Two_line_titles_are_taller_than_one_line_ones()
    {
        // 高度 = 基础高 + 间距 + 行数 × 行高（SquareStripeButton.kt:320-327）
        var one = Named("Terminal");
        var two = Named("Version Control");

        Assert.True(two.DesiredSize.Height > one.DesiredSize.Height);
    }

    [AvaloniaFact]
    public void The_stripe_takes_the_custom_width()
    {
        // ResizeStripeManager.kt:215-218 —— 打开时默认 59
        var toolbar = new ForgeToolWindowToolbar { Side = StripeSide.Left, CustomWidth = 59 };
        toolbar.TopGroup.Children.Add(Named("Project"));

        toolbar.Measure(new Size(double.PositiveInfinity, 600));

        Assert.Equal(59, toolbar.DesiredSize.Width);
    }

    [Theory]
    // ResizeStripeManager.kt:138-149 · checkMinMax
    [InlineData(10, false, 40)]
    [InlineData(10, true, 33)]
    [InlineData(59, false, 59)]
    [InlineData(500, false, 100)]
    public void The_custom_width_is_clamped(double width, bool compact, double expected)
        => Assert.Equal(expected, ForgeToolWindowToolbar.ClampCustomWidth(width, compact));

    [AvaloniaFact]
    public void The_frame_switches_the_whole_stripe_at_once()
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var manager = shell.Manager;
        var frame = new ForgeToolWindowFrame { ShowToolWindowNames = true };
        frame.Attach(shell);

        Assert.Equal(ForgeMetrics.StripeNamedDefaultWidth, frame.LeftToolbar.CustomWidth);
        Assert.All(
            frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>(),
            x => Assert.True(x.ShowName));

        frame.ShowToolWindowNames = false;

        Assert.Equal(0, frame.LeftToolbar.CustomWidth);
        Assert.All(
            frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>(),
            x => Assert.False(x.ShowName));
    }

    /// <summary>
    /// 显名时的高亮背景板要**包住图标和文字**，不是只包图标。
    /// </summary>
    /// <remarks>
    /// 上游 <c>SquareStripeButton.kt:196-198 · paintButtonLook</c>：<c>paintBackground</c> 收到的是
    /// button 本身（size 已含名字的高度），只有 <c>paintIcon</c> 才用 <c>buttonWrapper</c>
    /// 把高度砍掉一截去摆图标。本仓原来跟着图标一起砍，于是名字露在背景板外面 ——
    /// 看着像"选中的是那个图标"，而不是"选中的是这一项"。
    /// <para>按像素断言：名字那一行的中间必须已经被高亮铺到。</para>
    /// </remarks>
    [AvaloniaFact]
    public void 显名时的高亮包住图标和文字()
    {
        var button = new ForgeStripeButton
        {
            Title = "Project",
            ShowName = true,
            StripeWidth = ForgeMetrics.StripeNamedDefaultWidth,
            IsToolWindowVisible = true,
            Palette = ForgePalette.ExpUiLight,
        };

        var window = new Window
        {
            Width = 120,
            Height = 120,
            WindowDecorations = WindowDecorations.None,
            Background = Brushes.White,
            Content = new Panel { Children = { button }, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top },
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);

        // ⚠ 取样点要**避开文字笔画**：名字那一行的左边缘（文字居中，够不到那儿）。
        //   取中间的话，画上去的字本身就让像素变了色，旧实现也能"通过"——那是假绿。
        var labelY = (int)(ForgeMetrics.StripeButtonSize + ForgeMetrics.StripeIconLabelGap + 4);
        var iconY = (int)(ForgeMetrics.StripeButtonSize / 2);
        const int edgeX = 8;

        var onLabelRow = PixelProbe.At(bitmap!, edgeX, labelY);
        var onIconRow = PixelProbe.At(bitmap!, edgeX, iconY);
        var outside = PixelProbe.At(bitmap!, (int)button.Bounds.Width + 20, labelY);

        Assert.NotEqual(outside.ToUInt32(), onIconRow.ToUInt32());          // 图标那一行有高亮（前提）
        Assert.Equal(onIconRow.ToUInt32(), onLabelRow.ToUInt32());          // 名字那一行是同一块高亮
    }

    /// <summary>控件与 VM 两头对齐：菜单改控件要写回 VM，宿主改 VM 要反映到控件。</summary>
    /// <remarks>
    /// 持久化不归库管（宿主存在自己的外观设置里），但两头必须是同一个值 ——
    /// 否则宿主读到的是"菜单切之前"的那个。
    /// </remarks>
    [AvaloniaFact]
    public void 控件与VM的显示名字两头对齐()
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var frame = new ForgeToolWindowFrame();
        frame.Attach(shell);

        // 菜单那一路：改控件 → 落到 VM（否则存不下去）
        frame.ShowToolWindowNames = true;
        Assert.True(shell.ShowToolWindowNames);

        // 宿主那一路：改 VM → 控件跟上（启动时从设置里读回来就是这条）
        shell.ShowToolWindowNames = false;
        Assert.False(frame.ShowToolWindowNames);
        Assert.All(
            frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>(),
            x => Assert.False(x.ShowName));
    }

    private static ForgeStripeButton Named(string title)
    {
        var button = new ForgeStripeButton
        {
            Title = title,
            ShowName = true,
            StripeWidth = ForgeMetrics.StripeNamedDefaultWidth,
        };
        button.Measure(Size.Infinity);
        return button;
    }
}
