using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Piccolo.Forge.Themes;

/// <summary>
/// 树的层级缩进：把 <c>TreeViewItem.Level</c> × <c>Forge.Tree.Indent</c> 折成一条**左**内缩。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么缩进要画在行内部，而不是把子树整块推开。</b>
/// 缩进曾经是 <c>ItemsPresenter</c> 上的一条 <c>Margin</c>（<c>Forge.Tree.ChildMargin</c>）——
/// 那样连**行的高亮底**（<c>Border#PART_Header</c>）也一起被推走，于是选中条每深一级就短一截，
/// 三层树累计短 36。上游不是这个样子：<c>com.intellij.ui.tree.ui.DefaultTreeUI</c> 画选中背景时
/// 取的是**整行**的宽度（<c>paintRowBackground</c> 用 <c>tree.getWidth()</c>，不是节点自己的 bounds），
/// 缩进只作用于内容。Avalonia 自带的 Fluent 主题同理（<c>TreeViewItemIndent</c> × <c>Level</c>）。
/// </para>
/// <para>
/// ⚠ 缩进量必须**跟着主题走**（两族的 <c>Forge.Tree.Indent</c> 各自定义、宿主还能覆盖），
/// 所以它是 <c>MultiBinding</c> 的第二路 <c>DynamicResource</c>，不是这里的常量。
/// 也正因为要"层级 × 主题值"两路相乘，XAML 里没有等价写法 —— 这是本库唯一的转换器。
/// </para>
/// </remarks>
internal sealed class ForgeTreeIndentConverter : IMultiValueConverter
{
    /// <inheritdoc />
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // 模板刚套上、DynamicResource 还没解出来时，两路都可能是 UnsetValue —— 那时按 0 画，
        // 等资源到位会再算一次。这里**不能**抛：转换器抛异常在 Avalonia 里只落一条日志，
        // 表现成"某一层莫名不缩进"，比 0 更难查。
        if (values.Count < 2 || values[0] is not int level || values[1] is not double indent)
            return default(Thickness);

        return new Thickness(level * indent, 0, 0, 0);
    }
}
