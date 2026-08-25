using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Themes;
using Xunit;
using Path = Avalonia.Controls.Shapes.Path;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 输入框里那两个指示箭头（下拉的 chevron、数字框的上下箭头）的常态与悬停。
/// </summary>
/// <remarks>
/// 宿主实机反馈（2026-08-25）：箭头又粗又黑，压过框里的值本身；数字框悬停时还会铺一层
/// 按钮式底板，像框里凭空多出个按钮。改成：
/// <list type="bullet">
/// <item>常态与边框同粗同色（<c>Forge.Input.Border</c>）—— 它是指示不是内容；</item>
/// <item>悬停不铺底板，改由**被指的那一个**箭头自己变粗变深。</item>
/// </list>
/// <para>
/// ⚠ 数字框那条选择器必须写成 <c>/template/ RepeatButton:pointerover Path</c>：
/// 箭头是 RepeatButton 的**内容**，写成 <c>/template/ Path</c> 选不中它 ——
/// 选空不报错，症状是"改了没反应"。这条用例就是钉它的（写错当场红）。
/// </para>
/// </remarks>
public class InputGlyphTests
{
    [AvaloniaFact]
    public void 常态箭头与边框同粗同色()
    {
        using var theme = ForgeThemeScope.Use(ForgeThemeFamily.Islands);
        var (window, combo, numeric) = Host();

        var border = ResourceColor(combo, "Forge.Input.Border");
        var thickness = Resource<double>(combo, "Forge.Input.GlyphThickness");

        Assert.Equal(thickness, Chevron(combo).StrokeThickness);
        Assert.Equal(border, ColorOf(Chevron(combo).Stroke));

        foreach (var arrow in Arrows(numeric))
        {
            Assert.Equal(thickness, arrow.StrokeThickness);
            Assert.Equal(border, ColorOf(arrow.Stroke));
        }

        window.Close();
    }

    /// <summary>鼠标停在上箭头**那一块**的角上：只有它变粗变深，另一个不动，不铺底板，且触发区仍是整块。</summary>
    [AvaloniaFact]
    public void 悬停时只有被指的那个箭头变且不铺底板()
    {
        using var theme = ForgeThemeScope.Use(ForgeThemeFamily.Islands);
        var (window, _, numeric) = Host();

        var up = numeric.GetVisualDescendants().OfType<RepeatButton>()
            .Single(x => x.Name == "PART_IncreaseButton");
        var down = numeric.GetVisualDescendants().OfType<RepeatButton>()
            .Single(x => x.Name == "PART_DecreaseButton");

        // ★ 故意点在按钮的**角上**，离那条 1px 的箭头线远远的。★
        //   触发区必须是整块按钮，不是那条线 —— 否则用户得去命中 1px，实际用起来很别扭。
        //   去掉悬停底板时最容易顺手把底盘也去掉，那样触发区就真的缩成一条线了（宿主提醒）。
        var corner = up.TranslatePoint(new Point(2, 2), window)!.Value;
        window.MouseMove(corner);
        Relayout(window);

        var hoverThickness = Resource<double>(numeric, "Forge.Input.GlyphHoverThickness");
        var hoverColor = ResourceColor(numeric, "Forge.Text.Secondary");
        var idleThickness = Resource<double>(numeric, "Forge.Input.GlyphThickness");

        // 按钮本身够大：不是"只有那条线可点"
        Assert.True(up.Bounds.Width >= 10, $"箭头按钮太窄：{up.Bounds.Width}");
        Assert.True(up.Bounds.Height >= 10, $"箭头按钮太矮：{up.Bounds.Height}");
        Assert.True(up.IsPointerOver, "点在按钮角上就该算悬停 —— 触发区是整块，不是那条 1px 的线");
        Assert.Equal(hoverThickness, Arrow(up).StrokeThickness);
        Assert.Equal(hoverColor, ColorOf(Arrow(up).Stroke));

        // 另一个纹丝不动 —— 上下两颗各管各的
        Assert.False(down.IsPointerOver);
        Assert.Equal(idleThickness, Arrow(down).StrokeThickness);

        // ★ 没有按钮那种底板：悬停时叠加层仍是透明的
        var overlay = up.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "PART_Overlay");
        Assert.Equal(Colors.Transparent, ColorOf(overlay.Background));

        window.Close();
    }

    [AvaloniaFact]
    public void 下拉箭头悬停时变粗变深()
    {
        using var theme = ForgeThemeScope.Use(ForgeThemeFamily.Islands);
        var (window, combo, _) = Host();

        var point = combo.TranslatePoint(new Point(combo.Bounds.Width - 12, combo.Bounds.Height / 2), window)!.Value;
        window.MouseMove(point);
        Relayout(window);

        Assert.True(combo.IsPointerOver);
        Assert.Equal(Resource<double>(combo, "Forge.Input.GlyphHoverThickness"), Chevron(combo).StrokeThickness);
        Assert.Equal(ResourceColor(combo, "Forge.Text.Secondary"), ColorOf(Chevron(combo).Stroke));

        window.Close();
    }

    private static (Window Window, ComboBox Combo, NumericUpDown Numeric) Host()
    {
        var combo = new ComboBox { Width = 110, ItemsSource = new[] { "5" }, SelectedIndex = 0 };
        var numeric = new NumericUpDown { Width = 110, Value = 100 };
        var panel = new StackPanel { Spacing = 12, Children = { combo, numeric } };
        var window = new Window { Content = panel, Width = 220, Height = 140 };

        window.Show();
        Relayout(window);
        return (window, combo, numeric);
    }

    private static Path Chevron(ComboBox combo)
        => combo.GetVisualDescendants().OfType<Path>().Single(x => x.Name == "PART_Chevron");

    private static Path[] Arrows(NumericUpDown numeric)
        => [.. numeric.GetVisualDescendants().OfType<Path>()];

    private static Path Arrow(RepeatButton button)
        => button.GetVisualDescendants().OfType<Path>().Single();

    private static Color? ColorOf(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    /// <summary>
    /// 取主题里的键值。
    /// </summary>
    /// <remarks>
    /// ⚠ 必须带上 <c>ActualThemeVariant</c>：颜色类的键住在 <c>ThemeDictionaries</c> 里，
    /// 不带变体查回来的是 <c>UnsetValue</c> —— 断言会变成"期望 null"，看着像取色错了，
    /// 其实是查法不对。
    /// </remarks>
    private static T Resource<T>(Control control, string key)
        => (T)control.FindResource(control.ActualThemeVariant, key)!;

    /// <summary>颜色类的键给的是画刷（<c>SolidColorBrush</c>），取它的 <c>Color</c> 才能与画出来的比。</summary>
    private static Color? ResourceColor(Control control, string key)
        => (control.FindResource(control.ActualThemeVariant, key) as ISolidColorBrush)?.Color;

    private static void Relayout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
    }
}
