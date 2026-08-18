using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
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

    /// <summary>落点标签：「移至 左侧 底部」那一条，摆在图标块左边。</summary>
    /// <remarks>
    /// 对应上游拖动时挂在拖影旁边的 <c>HelpTooltip</c>
    /// （<c>ToolWindowDragHelper.kt:346-352</c>，文案 <c>"Move to " + anchor</c>）。
    /// 拖标题条时只靠一块图标分不清"松手会落到哪一格"，这条字就是答案。
    /// </remarks>
    private readonly TextBlock _label = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        IsVisible = false,
    };

    private readonly Border _labelFrame;
    private readonly StackPanel _root;
    private readonly RenderTargetBitmap? _tabThumb;
    private readonly RenderTargetBitmap? _windowThumb;
    /// <summary>预览类形态（标签 / 浮窗缩略）的透明度（<c>ToolWindowDragHelper.kt:94 · THUMB_OPACITY</c> 那一档）。</summary>
    private const double PreviewOpacity = 0.75;

    private readonly Size _tile;
    private readonly IBrush? _tileBackground;
    private readonly IBrush _defaultBackground;
    private readonly IBrush _labelForeground;
    private bool _labelOnRight;
    private readonly Color _stripeBackground;
    private readonly CornerRadius _tileCorner;
    private bool _floating;

    /// <summary>建一个示意窗口。</summary>
    /// <param name="thumb">被拖那一项（标签）的快照；没有就只画一个空框。</param>
    /// <param name="palette">配色 —— 边框与底色跟着它走。</param>
    /// <param name="windowThumb">源组整块的快照 —— 浮动预览形态用；不给就退回标签快照。</param>
    /// <param name="tile">
    /// 给了就用「图标块」形态：<paramref name="thumb"/> **居中**摆在这么大的一个圆角背板里，
    /// 背板尺寸固定不变。条带按钮走这一档（Rider 同款：跟手的就是那个图标，
    /// 不是整颗按钮的快照）；标签类的拖影不给，保持贴合快照的样子。
    /// <para>
    /// 尺寸要与落点方块**一样大**——两者说的是同一件事：这个图标松手后占的那一格。
    /// 所以这里传按钮自己的尺寸，落点方块也画成一整格（见 <c>ForgeControl.UpdateDropIndicator</c>）。
    /// </para>
    /// </param>
    /// <param name="tileBackground">图标块的底色；不给就用编辑区底色。</param>
    /// <param name="tileCorner">
    /// 图标块的圆角。给按钮那份（<c>ForgeSquareButtonBase.ArcCornerRadius</c>），
    /// 拖影才跟图标按钮长一个样 —— 宿主改了圆角，这里跟着改，不留第二个常量。
    /// </param>
    public ForgeDragGhost(
        RenderTargetBitmap? thumb,
        ForgePalette palette,
        RenderTargetBitmap? windowThumb = null,
        Size tile = default,
        IBrush? tileBackground = null,
        CornerRadius tileCorner = default)
    {
        ArgumentNullException.ThrowIfNull(palette);

        _tabThumb = thumb;
        _windowThumb = windowThumb;
        _tile = tile;
        _tileBackground = tileBackground;
        _stripeBackground = palette.StripeBackground;
        _tileCorner = tileCorner;
        _defaultBackground = new SolidColorBrush(palette.EditorBackground);
        _labelForeground = new SolidColorBrush(palette.Foreground);
        _frame = new Border
        {
            Background = _defaultBackground,
            BorderBrush = new SolidColorBrush(palette.HeaderTabUnderline),
            BorderThickness = new Thickness(1),
            Child = _thumb,
        };

        WindowDecorations = WindowDecorations.None;
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;
        Background = Brushes.Transparent;

        // ⚠ 窗口**要不到**任意小的尺寸：给 24×24，平台给回来的是 32×39（实测）。
        //   所以可见的那块背板不靠窗口尺寸表达，而是窗口里一个定尺元素，
        //   窗口多出来的边角透明——见 ApplyMode 的图标块那一档。
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        // 透明度按形态给（见 ApplyMode）：预览类的半透明，图标块不透明。
        Opacity = PreviewOpacity;

        // ⚠ 不能吃鼠标：指针此刻被拖动那边捕获着，示意窗口一旦参与命中测试，
        //   光标底下就永远是它，落点判定全废。
        IsHitTestVisible = false;

        _labelFrame = new Border
        {
            Background = _defaultBackground,
            BorderBrush = new SolidColorBrush(palette.HeaderTabUnderline),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 4),
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            Child = _label,
        };

        _root = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            Children = { _labelFrame, _frame },
        };

        Content = _root;
        ApplyMode();
    }

    /// <summary>
    /// 把一份可能**半透明**的底色压实成不透明：按它在条带上的实际观感，
    /// 与条带底色做一次 alpha 合成。
    /// </summary>
    /// <remarks>
    /// 按钮的悬停底色本来就是一层半透明的叠加（主题里就是这么给的），
    /// 直接拿去当拖影的底，界面会从拖影里透上来 —— 看着像影子，不像被拎起来的那颗按钮。
    /// 这里合成一次：颜色与"鼠标停在条带上那颗按钮"时看到的完全一致，但它是实心的。
    /// </remarks>
    /// <param name="brush">要压实的画刷；不是纯色就原样返回。</param>
    /// <param name="under">压在下面的底色（条带底）。</param>
    /// <returns>压实之后的画刷。</returns>
    private static IBrush? Opaque(IBrush? brush, Color under)
    {
        if (brush is not ISolidColorBrush solid || solid.Color.A == byte.MaxValue)
        {
            return brush;
        }

        var top = solid.Color;
        var a = top.A / 255d;

        return new SolidColorBrush(Color.FromRgb(
            (byte)Math.Round((top.R * a) + (under.R * (1 - a))),
            (byte)Math.Round((top.G * a) + (under.G * (1 - a))),
            (byte)Math.Round((top.B * a) + (under.B * (1 - a)))));
    }

    /// <summary>
    /// 落点标签的文案；<see langword="null"/> 表示不显示。
    /// </summary>
    /// <remarks>
    /// 只在「图标块」那一档有意义（浮动预览自己就说清了"会浮出一个窗口"）。
    /// 文案由宿主给（<c>ForgeMenuBuilder.DescribeMoveTarget</c>），这里只管摆。
    /// </remarks>
    /// <param name="text">要显示的字；<see langword="null"/> / 空 = 不显示。</param>
    /// <param name="onRight">
    /// 标签挂在图标块的**右**边还是左边。上游按目标边翻面
    /// （<c>ToolWindowDragHelper.kt:361-368</c>）：落到**左**侧的目标挂右边，其余挂左边 ——
    /// 否则标签会伸到屏幕外或压在它要指的那条边上。
    /// </param>
    public void SetLabel(string? text, bool onRight = false)
    {
        var show = !string.IsNullOrEmpty(text) && !_floating;

        // 换边就重排两个孩子的次序
        var wantIndex = onRight ? 1 : 0;
        if (_root.Children.IndexOf(_labelFrame) != wantIndex)
        {
            _root.Children.Remove(_labelFrame);
            _root.Children.Insert(wantIndex, _labelFrame);
        }

        _labelOnRight = onRight;
        _label.Text = text;
        _label.IsVisible = show;
        _labelFrame.IsVisible = show;

        if (show)
        {
            _label.Foreground = _labelForeground;
        }

        Resize();
    }

    /// <summary>
    /// 按当前形态定窗口尺寸。
    /// </summary>
    /// <remarks>
    /// ⚠ 挂了标签就**不能**再把窗口钉成图标块那么大：内容一下变成"标签 + 间距 + 图标块"，
    /// 钉住尺寸的结果是标签被裁掉半截（实机看到的是「移至 左」）。那一档交给
    /// <see cref="SizeToContent"/> 去量。
    /// <para>
    /// 不挂标签时仍然钉尺寸——那是"背板尺寸不能变"那条硬约束，且窗口本来就要不到 24×24
    /// （平台最小 32×39），靠 Border 定尺、窗口余下部分透明。
    /// </para>
    /// </remarks>
    private void Resize()
    {
        if (_floating)
        {
            return;
        }

        if (_labelFrame.IsVisible)
        {
            // ⚠ 先把显式尺寸撤了：Width/Height 是**盖过** SizeToContent 的，
            //   不撤的话窗口还钉在图标块那么大，标签被裁掉半截
            //   （实机：第一次挂标签就是「移至 左」，拖去浮动那一档改过尺寸再回来才正常）。
            Width = double.NaN;
            Height = double.NaN;
            SizeToContent = SizeToContent.WidthAndHeight;
            return;
        }

        SizeToContent = SizeToContent.Manual;

        if (_tile != default)
        {
            Width = _tile.Width;
            Height = _tile.Height;
        }
    }

    /// <summary>现在是不是"小窗口预览"那一档。</summary>
    public bool IsFloatPreview => _floating;

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

    /// <summary>
    /// 把框恢复成"没有任何形态特有设定"的样子。
    /// </summary>
    /// <remarks>
    /// ⚠ 每一档开头都要先复位：形态是**来回切**的，只写自己那一档的设定，
    /// 上一档留下的就成了下一档的暗坑 —— 图标块那档把框钉成了 24×24，
    /// 切到浮动预览时缩略图当场被裁在那个尺寸里（用户实机提的）。
    /// </remarks>
    private void ResetFrame()
    {
        _frame.Width = double.NaN;
        _frame.Height = double.NaN;
        _frame.HorizontalAlignment = HorizontalAlignment.Stretch;
        _frame.VerticalAlignment = VerticalAlignment.Stretch;
        _frame.Background = _defaultBackground;
        _frame.BorderThickness = new Thickness(1);
        _frame.Padding = default;
        _frame.CornerRadius = default;

        _thumb.Width = double.NaN;
        _thumb.Height = double.NaN;
        _thumb.Stretch = Stretch.None;
        _thumb.HorizontalAlignment = HorizontalAlignment.Stretch;
        _thumb.VerticalAlignment = VerticalAlignment.Stretch;

        Opacity = PreviewOpacity;
    }

    /// <summary>按当前形态摆好图与框。</summary>
    private void ApplyMode()
    {
        ResetFrame();

        _label.IsVisible = false;
        _labelFrame.IsVisible = false;

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
            SizeToContent = SizeToContent.Manual;
            Width = _thumb.Width + 6;
            Height = _thumb.Height + 6;
            return;
        }

        // 图标块形态：一块尺寸固定的圆角背板，图标居中 —— 跟手的就是"这个图标"，
        // 而不是整颗按钮的快照（用户按 Rider 实机提的）。
        // ⚠ **不描边**：那圈强调色（HeaderTabUnderline）在这一档看着就是"选中了一个东西"，
        //   而拖影不是选中态；Rider 那个也是一块纯色圆角板子。
        if (_tile != default)
        {
            _thumb.Source = _tabThumb;
            _thumb.HorizontalAlignment = HorizontalAlignment.Center;
            _thumb.VerticalAlignment = VerticalAlignment.Center;

            // 快照比背板大就等比收进去（取不到图标控件时拍的是整颗按钮，那张图比背板大）——
            // 不收的话 Stretch.None 会让它按原尺寸画、被背板裁掉一圈。
            var thumb = _tabThumb?.Size ?? default;
            if (thumb.Width > _tile.Width || thumb.Height > _tile.Height)
            {
                var scale = Math.Min(_tile.Width / thumb.Width, _tile.Height / thumb.Height);
                _thumb.Stretch = Stretch.Uniform;
                _thumb.Width = Math.Max(1, thumb.Width * scale);
                _thumb.Height = Math.Max(1, thumb.Height * scale);
            }
            else
            {
                _thumb.Stretch = Stretch.None;
                _thumb.Width = double.NaN;
                _thumb.Height = double.NaN;
            }

            _frame.CornerRadius = _tileCorner;
            _frame.BorderThickness = default;
            _frame.Background = Opaque(_tileBackground, _stripeBackground) ?? _frame.Background;

            // ★ 图标块**不透明**：它不是"预览"，就是被拎起来的那颗图标按钮本身，
            //   半透明会让底下的界面透上来，看着像个影子（用户实机提的）。
            //   标签 / 浮窗那两档仍是半透明 —— 那两个说的是"松手会得到什么"，是预览。
            Opacity = 1;

            // 背板尺寸钉在这块 Border 上（窗口要不到这么小，见构造里那条），
            // 靠左上摆，于是"窗口原点 = 背板左上角"，抓握点的算式不用改。
            _frame.Width = _tile.Width;
            _frame.Height = _tile.Height;
            _frame.HorizontalAlignment = HorizontalAlignment.Left;
            _frame.VerticalAlignment = VerticalAlignment.Top;

            Resize();
            return;
        }

        _frame.BorderThickness = new Thickness(1);

        // 标签形态：贴住快照的尺寸（只加边框那 2px）—— 窗比快照宽的话，
        // Stretch.None 的图会在窗里**居中**，视觉上与抓握点错开半截。
        _thumb.Source = _tabThumb;
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
        var g = _floating
            ? new Vector(Width / 2, 12)
            : _labelFrame.IsVisible
                // 图标块在光标下，标签甩在左边。宽度取实际排出来的那个（SizeToContent 之后
                // Width 要等下一次排布才更新，直接用会差一帧、拖影抖一下）。
                // 图标块在光标下：它挂在哪一边，抓握点就从哪一边算
                ? _labelOnRight
                    ? new Vector(_tile.Width / 2, _tile.Height / 2)
                    : new Vector(
                        (_root.Bounds.Width > 0 ? _root.Bounds.Width : Width) - (_tile.Width / 2),
                        _tile.Height / 2)
                : grab;
        Position = new PixelPoint(screen.X - (int)g.X, screen.Y - (int)g.Y);
    }
}
