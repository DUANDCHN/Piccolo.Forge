using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 跨窗口拖动时跟着光标走的那个示意窗口。
/// </summary>
/// <remarks>
/// 窗口内拖动的拖影画在编辑区自己的覆盖层上（<see cref="ForgeDragOverlay"/>），
/// 可**一旦指针离开这个窗口，覆盖层就画不到那儿去了** —— 表现就是"拖出去之后只剩一个光标"。
/// 所以出了窗口改用一个真窗口当拖影：无边框、置顶、不进任务栏、不吃鼠标。
/// <para>
/// 对位上游拖出编辑区时那个半透明的窗口预览（<c>DockManagerImpl</c> 的 drag image）。
/// 里面画的是被拖标签的快照，外面套一圈边 —— 告诉你松手会得到一个多大的窗口。
/// </para>
/// </remarks>
internal sealed class ForgeDragGhost : Window
{
    private readonly Image _thumb = new() { Stretch = Stretch.None };
    private readonly Border _frame;

    /// <summary>建一个示意窗口。</summary>
    /// <param name="thumb">被拖那一项的快照；没有就只画一个空框。</param>
    /// <param name="palette">配色 —— 边框与底色跟着它走。</param>
    public ForgeDragGhost(RenderTargetBitmap? thumb, ForgePalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);

        _thumb.Source = thumb;
        _frame = new Border
        {
            Background = new SolidColorBrush(palette.EditorBackground),
            BorderBrush = new SolidColorBrush(palette.HeaderTabUnderline),
            BorderThickness = new Thickness(1),
            Child = _thumb,
        };

        WindowDecorations = WindowDecorations.None;
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;
        Background = Brushes.Transparent;
        Opacity = 0.75;

        // ⚠ 不能吃鼠标：指针此刻被拖动那边捕获着，示意窗口一旦参与命中测试，
        //   光标底下就永远是它，落点判定全废。
        IsHitTestVisible = false;

        Width = Math.Max(120, thumb?.Size.Width ?? 160);
        Height = Math.Max(28, thumb?.Size.Height ?? 28);
        Content = _frame;
    }

    /// <summary>挪到光标处（屏幕坐标），抓握点保持在按下时那个位置。</summary>
    /// <param name="screen">光标位置。</param>
    /// <param name="grab">抓握偏移。</param>
    public void MoveTo(PixelPoint screen, Vector grab)
        => Position = new PixelPoint(screen.X - (int)grab.X, screen.Y - (int)grab.Y);
}
