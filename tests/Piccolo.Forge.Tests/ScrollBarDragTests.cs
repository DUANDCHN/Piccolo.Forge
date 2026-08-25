using System.Linq;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 滚动条拖动的跟手性：拖动 d 像素，滑块必须也走 d 像素（1:1）。
/// </summary>
/// <remarks>
/// 这件事由 <c>Track</c> 的密度决定：<c>密度 = (轨道长 − 滑块长) ÷ 数值范围</c>，
/// 换算时 <c>Δ值 = Δ像素 × 密度</c>。只要密度里那个"滑块长"与**画出来的**滑块不是同一个数，
/// 拖起来就不跟手 —— 而滑块画多长受模板摆布（外边距、最小尺寸），所以这是主题库的账。
/// </remarks>
public class ScrollBarDragTests
{
    /// <summary>竖向：往下拖，值变大（0 在顶端）。</summary>
    [AvaloniaFact]
    public void Vertical_drag_down_increases_value()
    {
        var track = Layout(Orientation.Vertical);
        Assert.True(track.ValueFromDistance(0, 50) > 0, "往下拖应当让值变大");
    }

    /// <summary>横向：往右拖，值变大（0 在左端）。</summary>
    /// <remarks>
    /// 这条曾经是反的：模板里 <c>Track.IsDirectionReversed</c> 被**写死成 true**，
    /// 一份模板两个方向共用。竖向需要 true（值 0 在顶端），横向需要 false（值 0 在左端），
    /// 于是横向滚动条拖起来整个是反的。⚠ 别再图省事把这个属性写成常量。
    /// </remarks>
    [AvaloniaFact]
    public void Horizontal_drag_right_increases_value()
    {
        var track = Layout(Orientation.Horizontal);
        Assert.True(track.ValueFromDistance(50, 0) > 0, "往右拖应当让值变大");
    }

    private static Track Layout(Orientation orientation)
    {
        var bar = new ScrollBar
        {
            Orientation = orientation,
            Minimum = 0,
            Maximum = 900,
            ViewportSize = 100,
        };
        if (orientation == Orientation.Vertical) bar.Height = 400; else bar.Width = 400;

        var window = new Window { Content = bar, Width = 400, Height = 400 };
        window.Show();
        window.UpdateLayout();
        return bar.GetVisualDescendants().OfType<Track>().Single();
    }

    [AvaloniaFact]
    public void Vertical_thumb_travels_pixel_for_pixel_with_the_pointer()
    {
        var bar = new ScrollBar
        {
            Orientation = Orientation.Vertical,
            Minimum = 0,
            Maximum = 900,
            ViewportSize = 100,
            Height = 400,
        };

        var window = new Window { Content = bar, Width = 200, Height = 400 };
        window.Show();
        window.UpdateLayout();

        var track = bar.GetVisualDescendants().OfType<Track>().Single();
        var thumb = track.Thumb!;

        // 画出来的滑块（含外边距占的位置）能走的距离
        var travel = track.Bounds.Height - thumb.Bounds.Height;

        // Track 自己认为「一像素值多少」的倒数：拖满整条应当正好覆盖整个数值范围
        var valueForFullTravel = track.ValueFromDistance(0, travel);

        Assert.Equal(bar.Maximum - bar.Minimum, valueForFullTravel, 1);
    }
}
