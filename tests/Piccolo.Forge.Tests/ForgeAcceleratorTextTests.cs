using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 菜单项右侧那列快捷键提示：显示的是**键帽上印的样子**，不是 <c>Key</c> 枚举名；
/// 宿主可以用 <see cref="ForgeMenu.GestureTextProperty"/> 覆盖成自己的说法。
/// </summary>
/// <remarks>
/// 由来（2026-08-31）：宿主报菜单里写着 <c>Ctrl+OemComma</c>，认不出那是逗号。
/// 根因是模板原先 <c>Text="{TemplateBinding InputGesture}"</c>，把
/// <c>KeyGesture.ToString()</c> 的枚举名原样示人——"Oem" 是 Windows 扫描码的历史包袱。
/// </remarks>
public class ForgeAcceleratorTextTests
{
    [Theory]
    [InlineData("Ctrl+OemComma", "Ctrl+,")]
    [InlineData("Ctrl+OemPeriod", "Ctrl+.")]
    [InlineData("Ctrl+Shift+OemQuestion", "Ctrl+Shift+/")]
    [InlineData("Alt+OemMinus", "Alt+-")]
    [InlineData("Ctrl+Add", "Ctrl+Num+")]
    [InlineData("Ctrl+Return", "Ctrl+Enter")]
    [InlineData("Escape", "Esc")]
    public void 认不出的键名换成键帽形(string gesture, string expected)
        => Assert.Equal(expected, ForgeKeyGestureText.ToDisplay(gesture));

    /// <summary>本来就一眼认得的键不许改——改了只会让人对不上自己按的那颗。</summary>
    [Theory]
    [InlineData("Ctrl+O")]
    [InlineData("F5")]
    [InlineData("Ctrl+Shift+L")]
    public void 字母数字与功能键原样保留(string gesture)
        => Assert.Equal(gesture, ForgeKeyGestureText.ToDisplay(gesture));

    [Fact]
    public void 空手势得空串()
    {
        Assert.Equal(string.Empty, ForgeKeyGestureText.ToDisplay((KeyGesture?)null));
        Assert.Equal(string.Empty, ForgeKeyGestureText.ToDisplay(string.Empty));
    }

    /// <summary>
    /// <b>模板真的画出了键帽形。</b>只测转换器不够：模板改错了绑定（比如还绑着 InputGesture）
    /// 一样编译得过、一样全绿，而用户看到的仍是 <c>Ctrl+OemComma</c>。
    /// </summary>
    [AvaloniaFact]
    public void 模板画出的是键帽形而不是枚举名()
    {
        Assert.Equal("Ctrl+,", AcceleratorTextOf(new MenuItem
        {
            Header = "打开设置",
            InputGesture = KeyGesture.Parse("Ctrl+OemComma"),
        }));
    }

    /// <summary>宿主设了 <c>ForgeMenu.GestureText</c> 就用它，不再管 <c>InputGesture</c>。</summary>
    [AvaloniaFact]
    public void 自定义文本槽优先于手势()
    {
        var item = new MenuItem
        {
            Header = "打开设置",
            InputGesture = KeyGesture.Parse("Ctrl+OemComma"),
        };
        ForgeMenu.SetGestureText(item, "长按左键");

        Assert.Equal("长按左键", AcceleratorTextOf(item));
    }

    /// <summary>两路都没有时是空串，不是 "null" 之类的字面量。</summary>
    [AvaloniaFact]
    public void 两路都没有时那一列是空的()
        => Assert.Equal(string.Empty, AcceleratorTextOf(new MenuItem { Header = "没有快捷键" }));

    /// <summary>把菜单项挂进窗口、套上模板，取出 PART_Accelerator 上真正画出来的文本。</summary>
    private static string AcceleratorTextOf(MenuItem item)
    {
        using var theme = ForgeThemeScope.Use(ForgeThemeFamily.ExpUi);

        var window = new Window
        {
            Width = 300,
            Height = 200,
            Content = item,
            RequestedThemeVariant = ThemeVariant.Light,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();

        var accelerator = item.GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(t => t.Name == "PART_Accelerator");
        var text = accelerator.Text ?? string.Empty;

        // 空文本列必须退出布局，否则仍会留下快捷键前的 24px 空白；有文本时继续正常显示。
        Assert.Equal(!string.IsNullOrEmpty(text), accelerator.IsVisible);
        Assert.Equal(item.Padding.Left, item.Padding.Right);

        window.Close();
        return text;
    }
}
