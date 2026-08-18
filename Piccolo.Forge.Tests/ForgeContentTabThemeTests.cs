using System.Linq;
using Avalonia;
using Avalonia.Controls;
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
/// 内容标签做成**真模板**之后的判据（REQ-067）：颜色来自资源键、状态由伪类选、
/// 几何与原先自绘时逐条对得上。
/// </summary>
/// <remarks>
/// 与条带按钮那一片同一套做法，但多钉两件它独有的：
/// <list type="number">
/// <item>标题与图标是**两个前景**（标题走 <c>Foreground</c>、图标走 <c>IconBrush</c>）
/// —— 模板化之前标题的颜色根本没经过主题，是直接从 <c>Palette</c> 取的；</item>
/// <item>下划线的位置仍出自 <c>ComputeUnderlineRect</c>，只是改由模板里的 <c>Border</c> 拿它当 <c>Margin</c>。</item>
/// </list>
/// <para>
/// ⚠ 每条都**先挂进窗口**再断言：控件没进可视树时拿不到 <c>ControlTheme</c>，
/// 断言会落在兜底值上（见 <see cref="ThemeResourceSpikeTests"/> 的探路结论）。
/// </para>
/// </remarks>
public class ForgeContentTabThemeTests
{
    /// <summary>四处颜色（标题 / 图标 / 悬停底 / 下划线）都来自库自带的键。</summary>
    [AvaloniaFact]
    public void 标签的四处颜色都来自库自带的键()
    {
        var (window, tab) = Host(ThemeVariant.Dark, x => x.CanBeClosed = true);
        var palette = ForgePalette.ExpUiDark;

        // 标题走 Foreground，图标走 IconBrush —— 上游本来就是两个颜色
        Assert.Equal(palette.Foreground, ColorOf(Title(tab).Foreground));
        Assert.Equal(palette.ButtonForeground, ColorOf(Icon(tab).Brush));
        Assert.NotEqual(palette.Foreground, palette.ButtonForeground);

        Assert.Equal(palette.HeaderTabHoverBackground, ColorOf(Part(tab, "PART_Background").Background));

        // ⚠ 下划线这里取到的是 **inactive** 那档：IsWindowActive 默认 false（失焦）。
        //   两档各自的取色由 下划线选中才出且失焦换一档 钉。
        Assert.Equal(palette.HeaderTabInactiveUnderline, ColorOf(Part(tab, "PART_Underline").Background));

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
        using var theme = ForgeThemeScope.Use(ForgeThemeFamily.Islands);
        var (window, tab) = Host(ThemeVariant.Dark, x =>
        {
            x.CanBeClosed = true;
            x.Palette = ForgePalette.IslandsDark;
        });

        Assert.Equal(ForgePalette.IslandsDark.Foreground, ColorOf(Title(tab).Foreground));
        Assert.Equal(ForgePalette.IslandsDark.ButtonForeground, ColorOf(Icon(tab).Brush));
        Assert.Equal(
            ForgePalette.IslandsDark.HeaderTabHoverBackground,
            ColorOf(Part(tab, "PART_Background").Background));

        // 两族的悬停底本来就不同，否则这条用例没有判别力
        Assert.NotEqual(
            ForgePalette.ExpUiDark.HeaderTabHoverBackground,
            ForgePalette.IslandsDark.HeaderTabHoverBackground);

        window.Close();
    }

    /// <summary>
    /// 选中且**不止一块内容**时标题换成下划线那档；只有一块时不换。
    /// </summary>
    /// <remarks>
    /// <c>ContentTabLabel.java:199-203</c>。这条判定原先在 <c>MeasureText</c> 里
    /// （<c>IsSelected &amp;&amp; !IsSoleContent</c>），现在是选择器 <c>^:selected:not(:sole-content)</c>。
    /// </remarks>
    [AvaloniaFact]
    public void 选中且不止一块内容时标题换成下划线那档()
    {
        var (window, tab) = Host(ThemeVariant.Dark);
        var palette = ForgePalette.ExpUiDark;

        Assert.Equal(palette.Foreground, ColorOf(Title(tab).Foreground));

        tab.IsSelected = true;
        Relayout(window);
        Assert.Equal(palette.HeaderTabUnderline, ColorOf(Title(tab).Foreground));

        // 只有这一块内容时选中态不换前景
        tab.IsSoleContent = true;
        Relayout(window);
        Assert.Equal(palette.Foreground, ColorOf(Title(tab).Foreground));

        window.Close();
    }

    /// <summary>下划线：选中才出，窗口失焦换成 inactive 那档。</summary>
    [AvaloniaFact]
    public void 下划线选中才出且失焦换一档()
    {
        var (window, tab) = Host(ThemeVariant.Dark);
        var underline = Part(tab, "PART_Underline");
        var palette = ForgePalette.ExpUiDark;

        Assert.False(underline.IsVisible);

        tab.IsSelected = true;
        Relayout(window);
        Assert.True(underline.IsVisible);

        // IsWindowActive 默认 false（= 失焦），所以这一档才是初始状态
        Assert.Equal(palette.HeaderTabInactiveUnderline, ColorOf(underline.Background));

        tab.IsWindowActive = true;
        Relayout(window);
        Assert.Equal(palette.HeaderTabUnderline, ColorOf(underline.Background));

        Assert.NotEqual(palette.HeaderTabUnderline, palette.HeaderTabInactiveUnderline);

        window.Close();
    }

    /// <summary>
    /// 下划线的几何照抄原实现：贴底、高 3、圆角是主题 arc 的一半。
    /// </summary>
    /// <remarks>
    /// 位置仍由 <see cref="ForgeContentTab.ComputeUnderlineRect"/> 算
    /// （<c>JBDefaultTabPainter.kt:92-96</c> + <c>ToolWindowTabPainter.kt:35-36</c> 那两下抵消），
    /// 模板只是把它当 <c>Margin</c> 用 —— 这条钉的就是"搬家没搬歪"。
    /// </remarks>
    [AvaloniaFact]
    public void 下划线的位置仍出自ComputeUnderlineRect()
    {
        var (window, tab) = Host(ThemeVariant.Dark, x => x.IsSelected = true);
        var underline = Part(tab, "PART_Underline");
        var expected = ForgeContentTab.ComputeUnderlineRect(new Rect(tab.Bounds.Size));

        Assert.Equal(expected.Y, tab.UnderlineMargin.Top);
        Assert.Equal(expected.Y, underline.Bounds.Y);
        Assert.Equal(ForgeMetrics.HeaderTabUnderlineHeight, underline.Bounds.Height);
        Assert.Equal(tab.Bounds.Width, underline.Bounds.Width);
        Assert.Equal(41, underline.Bounds.Bottom);

        // 主题里的 arc 是直径，给 Avalonia 要折半
        Assert.Equal(new CornerRadius(ForgeMetrics.HeaderTabUnderlineArc / 2), tab.UnderlineCornerRadius);

        window.Close();
    }

    /// <summary>标题从左内边距 12 起、竖直居中；图标 12 见方、贴右缘 4。</summary>
    /// <remarks>
    /// 原先是 <c>DrawText(text, (Inset, (高 − 字高) / 2))</c> 与
    /// <c>(宽 − 4 − 12, (高 − 12) / 2, 12, 12)</c>（<c>ContentLabel.java:252-257</c>），
    /// 期望值一字未改，只是改由模板摆。
    /// <para>
    /// ⚠ 两处都落在**半像素**上（41 高里标题 12.5、图标 14.5）。模板里那两个元素
    /// 因此关掉了 <c>UseLayoutRounding</c> —— 开着会被按到 12 / 14，整块上移半像素。
    /// 这条用例就是钉这半像素的（同 <c>分隔线的位置照抄上游包括那半个像素</c>）。
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void 标题与图标的位置照抄原实现包括那半个像素()
    {
        var (window, tab) = Host(ThemeVariant.Dark, x => x.CanBeClosed = true);
        var title = Title(tab);
        var icon = Icon(tab);

        // 模板里那个 TextBlock 的高度必须与原先 FormattedText 量出来的一致，
        // 否则"竖直居中"这条算式虽然照搬了，落点还是会偏
        var formatted = new FormattedText(
            tab.Title!,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(tab.FontFamily),
            tab.TitleFontSize,
            Brushes.Transparent);
        Assert.Equal(formatted.Height, title.Bounds.Height, 3);

        Assert.Equal(ForgeContentTab.Inset, title.Bounds.X);
        Assert.Equal((ForgeMetrics.HeaderHeight - formatted.Height) / 2, title.Bounds.Y, 3);

        Assert.Equal(12, icon.Bounds.Width);
        Assert.Equal(12, icon.Bounds.Height);
        Assert.Equal(tab.Bounds.Width - ForgeMetrics.TabRightIconInset, icon.Bounds.Right);
        Assert.Equal((ForgeMetrics.HeaderHeight - 12) / 2, icon.Bounds.Y, 3);

        window.Close();
    }

    /// <summary>不可关就不画那个图标；固定住时画的是图钉而不是叉，且是填充的。</summary>
    /// <remarks><c>ContentTabLabel.java:157-159</c>、<c>:250-254</c>。</remarks>
    [AvaloniaFact]
    public void 图标按可关与固定两态换()
    {
        var (window, tab) = Host(ThemeVariant.Dark);
        var icon = Icon(tab);

        Assert.False(icon.IsVisible);

        tab.CanBeClosed = true;
        Relayout(window);
        Assert.True(icon.IsVisible);
        Assert.Same(ForgeIcons.Close, icon.Icon);
        Assert.False(icon.IsFilled);

        tab.IsPinned = true;
        Relayout(window);
        Assert.Same(ForgeIcons.Pin, icon.Icon);
        Assert.True(icon.IsFilled);

        window.Close();
    }

    /// <summary>悬停底只在**没选中**时出 —— 用真的鼠标移动来验，不是拨伪类。</summary>
    [AvaloniaFact]
    public void 悬停底只在没选中时出()
    {
        var (window, tab) = Host(ThemeVariant.Dark);
        var background = Part(tab, "PART_Background");

        Assert.False(background.IsVisible);

        window.MouseMove(tab.Bounds.Center);
        Relayout(window);
        Assert.True(tab.IsPointerOver);
        Assert.True(background.IsVisible);

        tab.IsSelected = true;
        Relayout(window);
        Assert.False(background.IsVisible);

        window.Close();
    }

    private static Color? ColorOf(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    private static Border Part(Control control, string name)
        => control.GetVisualDescendants().OfType<Border>().Single(x => x.Name == name);

    private static TextBlock Title(Control control)
        => control.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "PART_Title");

    private static ForgeGeometryIcon Icon(Control control)
        => control.GetVisualDescendants().OfType<ForgeGeometryIcon>().Single(x => x.Name == "PART_Icon");

    /// <summary>
    /// 把标签挂进窗口。
    /// </summary>
    /// <remarks>
    /// ⚠ 两处不能省：**靠左上摆**（否则窗口会把它拉成 200×200，几何断言全错），
    /// 以及**显式给高 41**（<c>MeasureOverride</c> 的高度直接取可用高度，
    /// 不给就成了窗口那 200）。
    /// </remarks>
    private static (Window Window, ForgeContentTab Tab) Host(ThemeVariant variant, Action<ForgeContentTab>? setup = null)
    {
        var tab = new ForgeContentTab
        {
            Title = "Local Changes",
            Height = ForgeMetrics.HeaderHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        setup?.Invoke(tab);

        var window = new Window
        {
            Width = 400,
            Height = 200,
            Content = tab,
            RequestedThemeVariant = variant,
        };

        window.Show();
        Relayout(window);
        return (window, tab);
    }

    private static void Relayout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
    }
}
