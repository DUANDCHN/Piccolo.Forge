using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 拖动全程跟着光标走的那个示意窗口（"拖动中的小窗口"）。
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
    private readonly RenderTargetBitmap? _tabThumb;
    private readonly RenderTargetBitmap? _windowThumb;
    private bool _floating;

    /// <summary>建一个示意窗口。</summary>
    /// <param name="thumb">被拖那一项（标签）的快照；没有就只画一个空框。</param>
    /// <param name="palette">配色 —— 边框与底色跟着它走。</param>
    /// <param name="windowThumb">源组整块的快照 —— 浮动预览形态用；不给就退回标签快照。</param>
    public ForgeDragGhost(RenderTargetBitmap? thumb, ForgePalette palette, RenderTargetBitmap? windowThumb = null)
    {
        ArgumentNullException.ThrowIfNull(palette);

        _tabThumb = thumb;
        _windowThumb = windowThumb;
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

        Content = _frame;
        ApplyMode();
    }

    /// <summary>
    /// 切换形态：拖影的样子 = **松手会发生什么**。
    /// </summary>
    /// <remarks>
    /// 有落点（行内换位 / 分屏并组 / 并进别的窗）→ 标签的样子；
    /// 谁都不接、松手会**浮出一个窗口** → 换成"小窗口预览"（Rider 同款：
    /// 圆角框里装源组整块的缩略图）。不切的话，用户分不清松手是浮动还是换位（实机反馈）。
    /// </remarks>
    /// <param name="floating">是不是"松手会浮出窗口"那一档。</param>
    public void SetFloatPreview(bool floating)
    {
        if (_floating == floating)
        {
            return;
        }

        _floating = floating;
        ApplyMode();
    }

    /// <summary>按当前形态摆好图与框。</summary>
    private void ApplyMode()
    {
        if (_floating && (_windowThumb ?? _tabThumb) is { } preview)
        {
            // 浮动预览：缩进 320×220 的框里（等比），圆角 + 边框 —— 长得像一个缩小的窗口
            var scale = Math.Min(1, Math.Min(320 / preview.Size.Width, 220 / preview.Size.Height));
            _thumb.Source = preview;
            _thumb.Stretch = Stretch.Uniform;
            _thumb.Width = Math.Max(1, preview.Size.Width * scale);
            _thumb.Height = Math.Max(1, preview.Size.Height * scale);
            _frame.CornerRadius = new CornerRadius(8);
            _frame.Padding = new Thickness(2);
            Width = _thumb.Width + 6;
            Height = _thumb.Height + 6;
            return;
        }

        // 标签形态：贴住快照的尺寸（只加边框那 2px）—— 窗比快照宽的话，
        // Stretch.None 的图会在窗里**居中**，视觉上与抓握点错开半截。
        _thumb.Source = _tabThumb;
        _thumb.Stretch = Stretch.None;
        _thumb.Width = double.NaN;
        _thumb.Height = double.NaN;
        _frame.CornerRadius = default;
        _frame.Padding = default;
        Width = (_tabThumb?.Size.Width ?? 160) + 2;
        Height = (_tabThumb?.Size.Height ?? 28) + 2;
    }

    /// <summary>挪到光标处（屏幕坐标），抓握点保持在按下时那个位置。</summary>
    /// <remarks>浮动预览比标签大一圈，按标签的抓握点会整个偏到一边 —— 那一档抓在顶部居中。</remarks>
    /// <param name="screen">光标位置。</param>
    /// <param name="grab">抓握偏移。</param>
    public void MoveTo(PixelPoint screen, Vector grab)
    {
        var g = _floating ? new Vector(Width / 2, 12) : grab;
        Position = new PixelPoint(screen.X - (int)g.X, screen.Y - (int)g.Y);
    }
}
