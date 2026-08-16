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
/// 没有模板机制，手工管子元素是安全的。按钮只负责告诉它「图标该放哪个矩形」
/// （<see cref="Placement"/>）——那仍然是按钮自己的度量学问。
/// </para>
/// <para>
/// 这么绕一道换来的是：点击、键盘、按下态全部由 <see cref="Button"/> 提供，
/// 不必自己实现"按下-松开算一次点击"（自己实现的那版做出过「侧边栏按钮有时要点两下」）。
/// </para>
/// </remarks>
internal sealed class ForgeIconHost : Control
{
    private Control? _icon;

    /// <summary>按钮说图标该放哪；不给就铺满。</summary>
    public Func<Size, Rect>? Placement { get; set; }

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
            var rect = Placement?.Invoke(finalSize) ?? new Rect(finalSize);
            _icon.Measure(rect.Size);
            _icon.Arrange(rect);
        }

        return finalSize;
    }
}
