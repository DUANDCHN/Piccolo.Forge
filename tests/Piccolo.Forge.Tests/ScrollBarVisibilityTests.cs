using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 滚动条滑块的明显度：常态填色不许再掉回上游那档"看不见"的透明度。
/// </summary>
/// <remarks>
/// <para>
/// <b>这是刻意偏离上游，别当漂移改回去。</b>上游那组是
/// <c>ScrollBar.Transparent.*</c>（浅色 <c>#33737373</c>＝20% 不透明度），表达的是 IDE 里
/// "浮在代码上、不抢注意力"的分寸。本软件的表格与面板是白底密排数据，20% 的灰压在白底上
/// 算出来约 <c>#E3E3E3</c>，实机就是看不见 —— 宿主 2026-08-28 反馈的正是这条。
/// </para>
/// <para>
/// 判据取<b>下限</b>不取等值：数值日后还可能再调，这条守的是"别掉回上游那一档"，
/// 不是"必须正好是这个数"。等值那件事由 <c>PaletteGeneratorSyncTests</c>
/// 与生成器 <c>scripts/gen-forge-palette.py</c> 的 <c>Own(...)</c> 一起管。
/// </para>
/// <para>
/// ⚠ 常态<b>不画描边</b>是有意的（试过 1px，描边色与填充色太近，4× 放大都看不出来），
/// 所以这里只钉填充与粗细，不钉描边键。
/// </para>
/// </remarks>
public class ScrollBarVisibilityTests
{
    /// <summary>常态滑块的不透明度下限。上游是 0x33（20%），那一档实机看不见。</summary>
    private const byte MinimumRestAlpha = 0x8C;

    [AvaloniaTheory]
    [InlineData(ForgeThemeFamily.ExpUi, false)]
    [InlineData(ForgeThemeFamily.ExpUi, true)]
    [InlineData(ForgeThemeFamily.Islands, false)]
    [InlineData(ForgeThemeFamily.Islands, true)]
    public void 常态滑块够不透明且悬停更深(ForgeThemeFamily family, bool dark)
    {
        var (window, probe) = Build(dark ? ThemeVariant.Dark : ThemeVariant.Light, family);

        var rest = Brush(probe, "Forge.ScrollBar.Thumb");
        var hover = Brush(probe, "Forge.ScrollBar.ThumbHover");

        Assert.True(rest.A >= MinimumRestAlpha,
            $"{family} {(dark ? "暗" : "亮")}：常态滑块 alpha={rest.A:X2}，低于下限 {MinimumRestAlpha:X2} —— " +
            "多半是被当成「与上游不一致」改回去了，见本文件注释。");

        // 悬停必须比常态更深，否则鼠标移上去反而变淡
        Assert.True(hover.A > rest.A,
            $"{family} {(dark ? "暗" : "亮")}：悬停 alpha={hover.A:X2} 不高于常态 {rest.A:X2}");

        window.Close();
    }

    /// <summary>
    /// 常态滑块宽度 = 厚度 − 两侧内缩 = 12 − 2×2 = 8px。
    /// </summary>
    /// <remarks>
    /// 内缩与填色是同一次调整的两半（只加深不加宽仍嫌细），所以一并钉住。
    /// ⚠ 这个键在两族的调色板里各有一份，且 <c>PaletteGeneratorSyncTests</c> 要求
    /// 它与生成器的 METRICS 块逐字相同 —— 改这里要一起改三处。
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(ForgeThemeFamily.ExpUi)]
    [InlineData(ForgeThemeFamily.Islands)]
    public void 常态滑块内缩是二(ForgeThemeFamily family)
    {
        var (window, probe) = Build(ThemeVariant.Light, family);

        Assert.True(probe.TryFindResource("Forge.ScrollBar.ThumbMargin", probe.ActualThemeVariant, out var raw));
        Assert.Equal(new Thickness(2), Assert.IsType<Thickness>(raw));

        Assert.True(probe.TryFindResource("Forge.ScrollBar.Thickness", probe.ActualThemeVariant, out var thickness));
        Assert.Equal(12d, Assert.IsType<double>(thickness));

        window.Close();
    }

    private static Color Brush(Control probe, string key)
    {
        Assert.True(probe.TryFindResource(key, probe.ActualThemeVariant, out var value), $"缺键：{key}");
        return Assert.IsType<SolidColorBrush>(value).Color;
    }

    private static (Window Window, Border Probe) Build(ThemeVariant variant, ForgeThemeFamily family)
    {
        var window = new Window { Width = 200, Height = 100, RequestedThemeVariant = variant };
        window.Styles.Add(new global::Avalonia.Styling.Styles
        {
            Resources = new ResourceDictionary
            {
                MergedDictionaries =
                {
                    new ResourceInclude((Uri?)null)
                    {
                        Source = new Uri($"avares://Piccolo.Forge.Themes.{family}/Controls.Colors.axaml"),
                    },
                },
            },
        });

        var probe = new Border();
        window.Content = probe;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, probe);
    }
}
