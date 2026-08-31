using System;
using System.Collections.Generic;
using System.Globalization;
using global::Avalonia.Data.Converters;
using global::Avalonia.Input;
using Piccolo.Forge.Avalonia;

namespace Piccolo.Forge.Themes;

/// <summary>
/// 菜单项右侧那列快捷键提示显示什么：<c>ForgeMenu.GestureText</c> 设了就用它，
/// 否则把 <c>MenuItem.InputGesture</c> 交给 <see cref="ForgeKeyGestureText"/> 渲染成键帽形。
/// </summary>
/// <remarks>
/// <para>
/// 两路合一必须在转换器里做：XAML 没有"A 为空则取 B"的写法，而
/// <c>FallbackValue</c> 只在**绑定失败**时生效，属性值是 null / 空串时它不介入。
/// </para>
/// <para>
/// 这是本库第二个转换器（另一个是 <see cref="ForgeTreeIndentConverter"/>）。
/// 加转换器不是常态：只有"两路输入合成一个值"这类 XAML 表达不了的才配。
/// </para>
/// </remarks>
internal sealed class ForgeAcceleratorTextConverter : IMultiValueConverter
{
    /// <inheritdoc />
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // 模板刚套上时两路都可能是 UnsetValue —— 按空串画，等值到位会再算一次。
        // 这里不能抛：转换器抛异常在 Avalonia 里只落一条日志，表现成"快捷键那列莫名空着"，比空串更难查。
        if (values.Count < 2) return string.Empty;

        if (values[0] is string custom && !string.IsNullOrEmpty(custom)) return custom;

        return values[1] is KeyGesture gesture ? ForgeKeyGestureText.ToDisplay(gesture) : string.Empty;
    }
}
