using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 条带按钮的底色由**模板**画，颜色来自资源键 —— REQ-067 的第一片，端到端。
/// </summary>
/// <remarks>
/// 钉三件事：库自带的默认值对得上、宿主重定义键能盖过去、盖过之后仍跟随明暗。
/// 这三件合起来才是"和 Dock / Semi 一样"的那个能力；缺最后一条就退回成
/// 「覆盖与跟随明暗二选一」，那正是本需求要修的病。
/// <para>
/// ⚠ 断言的是**主题喂给控件的画刷属性**，不是画出来的像素。原先探的是模板里那层
/// <c>Border#PART_Background</c>，后来发现底色不能放模板（控件自绘的名字会被它盖住，
/// 见 <c>ForgeSquareButtonBase.ResolveBackgroundBrush</c> 的注释），底色改回 <c>Render</c> 画，
/// 那层 Border 也就没有了。「画出来对不对」由渲染快照用例与真机人眼比对负责。
/// </para>
/// </remarks>
public class ForgeStripeButtonThemeTests
{
    /// <summary>三档底色都来自库自带的键。</summary>
    [AvaloniaFact]
    public void 三档底色都来自库自带的键()
    {
        var (window, button) = Host(ThemeVariant.Dark);
        var palette = ForgePalette.ExpUiDark;

        Assert.Equal(palette.ButtonHoverBackground, ColorOf(button.HoverBackgroundBrush));
        Assert.Equal(palette.ButtonPressedBackground, ColorOf(button.PressedBackgroundBrush));
        Assert.Equal(palette.ButtonSelectedBackground, ColorOf(button.SelectedBackgroundBrush));

        // 优先级（激活 > 可见 > 悬停 > 无）在 ResolveBackground 里，与画刷那套同源；
        // 这条钉住语义，画刷那三条钉住取色
        Assert.Null(button.ResolveBackground());

        button.IsToolWindowVisible = true;
        Assert.Equal(palette.ButtonPressedBackground, button.ResolveBackground());

        button.IsToolWindowActive = true;
        Assert.Equal(palette.ButtonSelectedBackground, button.ResolveBackground());

        window.Close();
    }

    /// <summary>Islands 走另一套键 —— 一族一套完整主题，换族 = 换单元 + 新建控件（REQ-068）。</summary>
    /// <remarks>
    /// ⚠ 不能像从前那样"给同一个控件换 <c>Palette</c> 再重排一次"：族已经不由伪类表达，
    /// 而换掉 Styles 单元**不会**让已挂树的控件重取 ControlTheme（实测 <see cref="ThemeSwapSpikeTests"/>）。
    /// 顺序反了不报错，只是断言莫名其妙地不成立。
    /// </remarks>
    [AvaloniaFact]
    public void 换族就换一套键()
    {
        var (window, button) = Host(ThemeVariant.Dark);
        Assert.Equal(ForgePalette.ExpUiDark.ButtonSelectedBackground, ColorOf(button.SelectedBackgroundBrush));
        window.Close();

        using var theme = ForgeThemeScope.Use(ForgeThemeFamily.Islands);
        var (islandsWindow, islands) = Host(ThemeVariant.Dark);
        islands.Palette = ForgePalette.IslandsDark;
        Relayout(islandsWindow);
        Assert.Equal(ForgePalette.IslandsDark.ButtonSelectedBackground, ColorOf(islands.SelectedBackgroundBrush));

        // 两族的选中色本来就不同，否则这条用例没有判别力
        Assert.NotEqual(
            ForgePalette.ExpUiDark.ButtonSelectedBackground,
            ForgePalette.IslandsDark.ButtonSelectedBackground);

        islandsWindow.Close();
    }

    /// <summary>
    /// <b>宿主重定义键即生效，且切明暗仍各取各的。</b>
    /// </summary>
    /// <remarks>
    /// REQ-067 的核心验收：在此之前宿主只能给一个 C# 取色回调，
    /// 而直接赋 <c>Palette</c> 会被判成"宿主自己管配色"、从此不再跟随系统明暗。
    /// </remarks>
    [AvaloniaFact]
    public void 宿主覆盖之后仍然跟随明暗()
    {
        var (window, button) = Host(ThemeVariant.Dark);

        var inDark = Color.Parse("#FF00FF00");
        var inLight = Color.Parse("#FFFF0000");
        window.Resources.ThemeDictionaries[ThemeVariant.Dark] = new ResourceDictionary
        {
            ["Forge.ExpUi.ButtonSelectedBackground"] = inDark,
        };
        window.Resources.ThemeDictionaries[ThemeVariant.Light] = new ResourceDictionary
        {
            ["Forge.ExpUi.ButtonSelectedBackground"] = inLight,
        };

        Relayout(window);
        Assert.Equal(inDark, ColorOf(button.SelectedBackgroundBrush));

        window.RequestedThemeVariant = ThemeVariant.Light;
        Relayout(window);
        Assert.Equal(inLight, ColorOf(button.SelectedBackgroundBrush));

        window.Close();
    }

    /// <summary>画刷的颜色；没给就是 <see langword="null"/>。</summary>
    private static Color? ColorOf(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    private static (Window Window, ForgeStripeButton Button) Host(ThemeVariant variant)
    {
        var button = new ForgeStripeButton
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        var window = new Window
        {
            Width = 200,
            Height = 200,
            Content = button,
            RequestedThemeVariant = variant,
        };

        window.Show();
        Relayout(window);
        return (window, button);
    }

    private static void Relayout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
    }
}
