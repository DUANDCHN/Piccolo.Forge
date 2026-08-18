using Avalonia;
using Avalonia.Controls;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 按钮模板里那个「放宿主图标控件」的槽。
/// </summary>
/// <remarks>
/// 存在的理由是一条 Avalonia 的硬规则：<b><see cref="Button"/> 自带模板，
/// 而每次 <c>ApplyTemplate</c> 都会清空控件的 <c>VisualChildren</c></b> ——
/// 按钮把宿主给的图标控件手工挂在自己身上，挂一次被扫一次，图标当场消失。
/// <para>
/// 所以图标不挂在按钮上，挂在**模板产出的这个槽**里：它是普通 <see cref="Control"/>，
/// 没有模板机制，手工管子元素是安全的。「图标该放哪个矩形」仍是按钮自己的度量学问，
/// 由这个槽**回头去问 <see cref="StyledElement.TemplatedParent"/>**。
/// <para>
/// ⚠ 从"按钮在构造里建模板、顺手把 <c>IconRect</c> 当委托塞进来"改成"槽自己问"，
/// 是 REQ-067 逼出来的：模板要写进 <c>ControlTheme</c>（XAML），XAML 里传不了方法组；
/// 而且控件在构造里给 <c>Template</c> 赋的是 local value，**会盖过 ControlTheme 里的那份**。
/// </para>
/// </para>
/// <para>
/// 这么绕一道换来的是：点击、键盘、按下态全部由 <see cref="Button"/> 提供，
/// 不必自己实现"按下-松开算一次点击"（自己实现的那版做出过「侧边栏按钮有时要点两下」）。
/// </para>
/// </remarks>
/// <remarks>
/// <b>公开的</b>：它出现在 <c>ControlTheme</c> 的模板里（XAML 只认得到公开类型），
/// 用例也要靠它判断「图标槽里有没有东西」。宿主不该直接 new 它。
/// </remarks>
[ForgeThemeless("按钮里的图标**槽**：只按 IconRect 摆位置，长相归宿主给的图标控件。")]
public sealed class ForgeIconHost : Control
{
    private Control? _icon;

    /// <summary>按钮说图标该放哪；问不到按钮就铺满。</summary>
    /// <remarks>模板里的槽，<c>TemplatedParent</c> 就是那个按钮。</remarks>
    private Rect Placement(Size size)
        => TemplatedParent is ForgeSquareButtonBase button ? button.IconPlacement(size) : new Rect(size);

    /// <summary>宿主给的图标控件。</summary>
    public Control? Icon
    {
        get => _icon;
        set
        {
            if (ReferenceEquals(_icon, value))
            {
                return;
            }

            if (_icon is not null)
            {
                VisualChildren.Remove(_icon);
                LogicalChildren.Remove(_icon);
            }

            _icon = value;

            if (_icon is not null)
            {
                VisualChildren.Add(_icon);
                LogicalChildren.Add(_icon);
            }

            InvalidateMeasure();
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // 尺寸由按钮定（它有整套条带度量），这里不参与——只把可用空间转给图标
        _icon?.Measure(availableSize);
        return default;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_icon is not null)
        {
            var rect = Placement(finalSize);
            _icon.Measure(rect.Size);
            _icon.Arrange(rect);
        }

        return finalSize;
    }
}
