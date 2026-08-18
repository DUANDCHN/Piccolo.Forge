using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 把一个 <see cref="Geometry"/> 图标画出来的小控件 —— 供模板当子元素用。
/// </summary>
/// <remarks>
/// <b>为什么要它</b>（REQ-067）：控件自身的 <c>Render</c> 画在**模板子元素之下**，
/// 所以只要图标还在按钮的 <c>Render</c> 里画，模板里就不能放任何不透明的东西
/// （放了就把图标盖住 —— 实机撞过一次，症状是选中的按钮只剩图标、名字没了）。
/// 把图标搬成子元素，模板才重新有得写。
/// <para>
/// <b>为什么不用 Avalonia 的 <c>Path</c></b>：那条路要重新对
/// <c>Stretch</c> 与描边缩放的行为，画出来未必与原先逐像素一致。这里直接调
/// <see cref="ForgeIcons.Draw"/> —— 和之前按钮里那句是**同一份代码**，
/// 缩放、居中、填充/描边的取舍分毫不变，于是"搬家"不会顺带改变长相。
/// </para>
/// </remarks>
[ForgeThemeless("图标的画刷与尺寸由**用它的那份模板**喂，它自己没有族差。")]
public sealed class ForgeGeometryIcon : Control
{
    /// <summary><see cref="Icon"/> 的属性定义。</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ForgeGeometryIcon, Geometry?>(nameof(Icon));

    /// <summary><see cref="Brush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<ForgeGeometryIcon, IBrush?>(nameof(Brush));

    /// <summary><see cref="IsFilled"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsFilledProperty =
        AvaloniaProperty.Register<ForgeGeometryIcon, bool>(nameof(IsFilled));

    static ForgeGeometryIcon()
        => AffectsRender<ForgeGeometryIcon>(IconProperty, BrushProperty, IsFilledProperty, InkScaleProperty);

    /// <summary>要画的图形；<see langword="null"/> 就什么都不画。</summary>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>用什么画。</summary>
    public IBrush? Brush
    {
        get => GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    /// <summary>
    /// 实心还是描边。
    /// </summary>
    /// <remarks>
    /// 圆点类图标是实心的（三个小方块），描边画出来会变成三个小方框；
    /// 线条类（隐藏那条横线）填充与描边一致，所以那类统一走填充。
    /// </remarks>
    public bool IsFilled
    {
        get => GetValue(IsFilledProperty);
        set => SetValue(IsFilledProperty, value);
    }

    /// <summary><see cref="InkScale"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> InkScaleProperty =
        AvaloniaProperty.Register<ForgeGeometryIcon, double>(nameof(InkScale), 1);

    /// <summary>
    /// 墨迹放大系数（1 = 上游原样）。
    /// </summary>
    /// <remarks>
    /// 上游这套图标画在 16 视框里、<b>四周自带留白</b>：⋮ 的墨迹只有 10 高（y 3→13）、
    /// 收起那条线 9 长、停靠那个方框 12。宿主若在同一排里放**图标包**的图标
    /// （字体形态的字形几乎撑满 em 盒，墨迹约 0.83），两者摆在一起就是一大一小。
    /// <para>
    /// 这个口子让宿主把库自带的这几个**按墨迹**放大到与自己那批同档，
    /// 而不是反过来把自己那批缩小 —— 哪种更好看是宿主的产品取舍，库不替它定。
    /// 默认 1：不设就是上游原样，与本需求之前逐像素一致。
    /// </para>
    /// <para>⚠ 放大的是**画进去的框**，不是墨迹包围盒 —— 各图标彼此的大小关系仍是上游那套。</para>
    /// </remarks>
    public double InkScale
    {
        get => GetValue(InkScaleProperty);
        set => SetValue(InkScaleProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (Icon is not { } icon || Brush is not { } brush)
        {
            return;
        }

        // 放大 = 把"画进去的那个框"撑大，中心不动（Draw 自己会居中）。
        // 不裁：控件默认 ClipToBounds=false，撑出 16 的那一点点照样画得出来。
        var scale = double.IsFinite(InkScale) && InkScale > 0 ? InkScale : 1;
        var bounds = new Rect(Bounds.Size);
        var box = bounds.Inflate(new Thickness(
            bounds.Width * (scale - 1) / 2,
            bounds.Height * (scale - 1) / 2));

        ForgeIcons.Draw(context, icon, box, brush, filled: IsFilled);
    }
}
