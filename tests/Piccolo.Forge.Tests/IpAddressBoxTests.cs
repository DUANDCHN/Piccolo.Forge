using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 验证四段分格 IP 输入框 <see cref="ForgeIpAddressBox"/> 的尺寸对齐、双向文本同步、钳位、粘贴拆填与键盘跳段交互。
/// </summary>
public class IpAddressBoxTests
{
    private static void Relayout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static TextBox Octet(ForgeIpAddressBox box, int index)
        => box.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == $"PART_Octet{index}");

    private static Color? ResourceColor(Control c, string key)
        => c.TryFindResource(key, c.ActualThemeVariant, out var v) && v is ISolidColorBrush b ? b.Color : null;

    [AvaloniaTheory]
    [InlineData(ForgeThemeFamily.ExpUi)]
    [InlineData(ForgeThemeFamily.Islands)]
    public void IpAddressBox_与TextBox等高且支持双向拆填钳位与状态边框(ForgeThemeFamily family)
    {
        using var theme = ForgeThemeScope.Use(family);

        var textBox = new TextBox { Width = 200, Text = "192.168.1.1" };
        var ipBox = new ForgeIpAddressBox { Width = 200, Text = "192.168.1.100" };
        var window = new Window
        {
            Width = 300,
            Height = 160,
            Content = new StackPanel { Spacing = 8, Children = { textBox, ipBox } },
        };
        window.Show();
        Relayout(window);

        try
        {
            Assert.Equal("---", ipBox.SegmentPlaceholder);
            Assert.Equal(textBox.Bounds.Height, ipBox.Bounds.Height, 1);
            Assert.Equal("192", Octet(ipBox, 0).Text);
            Assert.Equal("168", Octet(ipBox, 1).Text);
            Assert.Equal("1", Octet(ipBox, 2).Text);
            Assert.Equal("100", Octet(ipBox, 3).Text);

            // 整串 IP 粘贴拆填 + 超过 255 自动钳位
            Octet(ipBox, 0).Text = "10.0.300.25";
            Relayout(window);
            Assert.Equal("10", Octet(ipBox, 0).Text);
            Assert.Equal("0", Octet(ipBox, 1).Text);
            Assert.Equal("255", Octet(ipBox, 2).Text);
            Assert.Equal("25", Octet(ipBox, 3).Text);
            Assert.Equal("10.0.255.25", ipBox.Text);

            // 悬停高亮边框与错误态红框
            var root = ipBox.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Root");
            var pt = ipBox.TranslatePoint(new Point(ipBox.Bounds.Width / 2, ipBox.Bounds.Height / 2), window)!.Value;
            window.MouseMove(pt);
            Relayout(window);
            Assert.True(ipBox.IsPointerOver);
            Assert.Equal(ResourceColor(ipBox, "Forge.Input.HoverBorder"), (root.BorderBrush as ISolidColorBrush)?.Color);

            ipBox.Classes.Add("error");
            Relayout(window);
            Assert.Equal(ResourceColor(ipBox, "Forge.Input.Error"), (root.BorderBrush as ISolidColorBrush)?.Color);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void IpAddressBox_点号跳下一段且Backspace返回上一段()
    {
        using var theme = ForgeThemeScope.Use(ForgeThemeFamily.Islands);

        var ipBox = new ForgeIpAddressBox { Width = 200, Text = "192." };
        var window = new Window { Width = 260, Height = 100, Content = ipBox };
        window.Show();
        Relayout(window);

        try
        {
            var o0 = Octet(ipBox, 0);
            var o1 = Octet(ipBox, 1);
            o0.Focus();
            Relayout(window);
            Assert.True(o0.IsFocused);

            // 在第 0 段已有数字时按 '.' 跳入第 1 段
            o0.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.OemPeriod });
            Relayout(window);
            Assert.True(o1.IsFocused);

            // 在空的第 1 段光标 0 处按 Backspace 退回第 0 段
            o1.CaretIndex = 0;
            o1.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Back });
            Relayout(window);
            Assert.True(o0.IsFocused);
        }
        finally
        {
            window.Close();
        }
    }
}
