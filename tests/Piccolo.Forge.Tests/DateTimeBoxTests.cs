using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 验证分段日期框 <see cref="ForgeDateBox"/> 与分段时间框 <see cref="ForgeTimeBox"/> 的
/// 默认 D2/T1 外观、双向绑定、闰年与大小月天数钳位、ShowSeconds 列折叠及 ↑/↓ 与分隔键交互。
/// </summary>
public class DateTimeBoxTests
{
    private static void Relayout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static TextBox Part(Control host, int index)
        => host.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == $"PART_Part{index}");

    [AvaloniaTheory]
    [InlineData(ForgeThemeFamily.ExpUi)]
    [InlineData(ForgeThemeFamily.Islands)]
    public void DateBox_默认D2外观与闰年大小月钳位(ForgeThemeFamily family)
    {
        using var theme = ForgeThemeScope.Use(family);

        var textBox = new TextBox { Width = 200, Text = "ref" };
        var dateBox = new ForgeDateBox { Width = 200 };
        var window = new Window
        {
            Width = 300,
            Height = 160,
            Content = new StackPanel { Spacing = 8, Children = { textBox, dateBox } },
        };
        window.Show();
        Relayout(window);

        try
        {
            Assert.Equal(textBox.Bounds.Height, dateBox.Bounds.Height, 1);
            Assert.Equal("/", dateBox.Separator);
            Assert.Equal("YYYY", dateBox.YearPlaceholder);
            Assert.Equal("MM", dateBox.MonthPlaceholder);
            Assert.Equal("DD", dateBox.DayPlaceholder);

            // 选中日期回填三段与 Text
            dateBox.SelectedDate = new DateTime(2026, 11, 19);
            Relayout(window);
            Assert.Equal("2026", Part(dateBox, 0).Text);
            Assert.Equal("11", Part(dateBox, 1).Text);
            Assert.Equal("19", Part(dateBox, 2).Text);
            Assert.Equal("2026/11/19", dateBox.Text);

            // 粘贴 '-' 或 '/' 分隔文本 -> 非闰年 2 月 31 自动钳位到 28
            Part(dateBox, 0).Text = "2026-02-31";
            Relayout(window);
            Assert.Equal("2026", Part(dateBox, 0).Text);
            Assert.Equal("02", Part(dateBox, 1).Text);
            Assert.Equal("28", Part(dateBox, 2).Text);
            Assert.Equal(new DateTime(2026, 2, 28), dateBox.SelectedDate);

            // 按 '/' 跳入下一段
            var p0 = Part(dateBox, 0);
            var p1 = Part(dateBox, 1);
            p0.Focus();
            Relayout(window);
            p0.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Oem2 });
            Relayout(window);
            Assert.True(p1.IsFocused);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(ForgeThemeFamily.ExpUi)]
    [InlineData(ForgeThemeFamily.Islands)]
    public void TimeBox_支持三段与两段切换及上下步进(ForgeThemeFamily family)
    {
        using var theme = ForgeThemeScope.Use(family);

        var textBox = new TextBox { Width = 200, Text = "ref" };
        var timeBox = new ForgeTimeBox { Width = 200 };
        var window = new Window
        {
            Width = 300,
            Height = 160,
            Content = new StackPanel { Spacing = 8, Children = { textBox, timeBox } },
        };
        window.Show();
        Relayout(window);

        try
        {
            Assert.Equal(textBox.Bounds.Height, timeBox.Bounds.Height, 1);
            Assert.Equal(":", timeBox.Separator);
            Assert.Equal("--", timeBox.SegmentPlaceholder);
            Assert.True(timeBox.ShowSeconds);

            // 粘贴超出范围时间 -> 自动钳位 23:59:59
            Part(timeBox, 0).Text = "28:75:90";
            Relayout(window);
            Assert.Equal("23", Part(timeBox, 0).Text);
            Assert.Equal("59", Part(timeBox, 1).Text);
            Assert.Equal("59", Part(timeBox, 2).Text);
            Assert.Equal(new TimeSpan(23, 59, 59), timeBox.SelectedTime);

            // 第 0 段（23）按 Up 循环回绕到 00
            var hourBox = Part(timeBox, 0);
            hourBox.Focus();
            Relayout(window);
            hourBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Up });
            Relayout(window);
            Assert.Equal("00", hourBox.Text);
            Assert.Equal(new TimeSpan(0, 59, 59), timeBox.SelectedTime);

            // 隐藏秒 -> 秒格不可见且第 4 列归零，Text 变为 HH:mm
            timeBox.ShowSeconds = false;
            Relayout(window);
            var grid = timeBox.GetVisualDescendants().OfType<Grid>().First(g => g.Name == "PART_Grid");
            Assert.False(Part(timeBox, 2).IsVisible);
            Assert.Equal(0, grid.ColumnDefinitions[4].Width.Value);
            Assert.Equal("00:59", timeBox.Text);
            Assert.Equal(new TimeSpan(0, 59, 0), timeBox.SelectedTime);
        }
        finally
        {
            window.Close();
        }
    }
}
