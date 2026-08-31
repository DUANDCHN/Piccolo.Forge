using global::Avalonia;
using global::Avalonia.Controls;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 菜单项上的 Forge 扩展点。目前只有一个：右侧那列快捷键提示的**自定义文本槽**。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么要开这个槽。</b><see cref="MenuItem"/> 是 Avalonia 内置控件，Forge 只能给它模板、
/// 加不了实例属性，而模板里那列文本原先直接绑 <see cref="MenuItem.InputGesture"/>——
/// 也就是把 <see cref="global::Avalonia.Input.KeyGesture.ToString()"/> 的结果原样示人。
/// 宿主想显示别的（本地化的说法、非键盘触发的提示、或者一条根本不是 KeyGesture 的手势）
/// 就无处下手。WPF 早就有对位物 <c>MenuItem.InputGestureText</c>，这里用附加属性补上。
/// </para>
/// <para>
/// <b>与自动格式化的关系。</b>不设这个属性时，模板会把 <see cref="MenuItem.InputGesture"/> 交给
/// <see cref="ForgeKeyGestureText"/> 渲染成键帽形（<c>Ctrl+OemComma</c> → <c>Ctrl+,</c>），
/// 所以**什么都不做也是对的**；这个槽是给"默认渲染不合适"的那些场合留的后门，不是必填项。
/// </para>
/// <para>
/// 非静态类：<c>AvaloniaProperty.RegisterAttached</c> 要拿宿主类型当类型参数，
/// 而静态类型不能做类型参数（CS0718）——与 <c>ForgeAppearance</c> 里那处同因。
/// </para>
/// </remarks>
public class ForgeMenu
{
    /// <summary>
    /// 菜单项右侧那列显示的文本。设了就用它，不再管 <see cref="MenuItem.InputGesture"/>；
    /// 空串等同于未设。
    /// </summary>
    public static readonly AttachedProperty<string?> GestureTextProperty =
        AvaloniaProperty.RegisterAttached<ForgeMenu, Control, string?>("GestureText");

    /// <summary>读 <see cref="GestureTextProperty"/>。</summary>
    public static string? GetGestureText(Control control) => control.GetValue(GestureTextProperty);

    /// <summary>写 <see cref="GestureTextProperty"/>。</summary>
    public static void SetGestureText(Control control, string? value) => control.SetValue(GestureTextProperty, value);
}
