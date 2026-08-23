using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Templates;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 通知卡的可替换部件。目前只有一件：右上角那排**操作按钮**。
/// </summary>
/// <remarks>
/// <para>
/// 卡片不是宿主 new 出来的（<see cref="WindowNotificationManager"/> 内部造），
/// 所以宿主没有"拿到卡片改它"的时机 —— 只能靠**继承下来的附加属性**：
/// 设在 manager（或它任一祖先）上，各卡片建出来时自然带上。
/// </para>
/// <para>
/// 不设就是默认那两颗（⋮ 与 ✕）。设了就整排换掉 ——
/// 刻意不做"追加一颗"：那要约定顺序、间距、显隐规则，比整排交给宿主画更难说清。
/// </para>
/// </remarks>
public static class ForgeNotification
{
    /// <summary>
    /// 卡片右上角那排操作按钮的模板；模板的 <c>DataContext</c> 是
    /// <see cref="NotificationCard"/> 本身。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 于是模板里：关闭写 <c>{Binding Close}</c>，
    /// 你 Show 进去的那个视图模型写 <c>{Binding Content.Xxx}</c>。
    /// </para>
    /// <para>
    /// ⚠ <b>可继承</b>（<c>inherits: true</c>）—— 这是它能用的前提，别去掉。
    /// 设在 <see cref="ForgeNotificationManager"/> 上即可覆盖它弹出的所有卡片。
    /// </para>
    /// <para>
    /// ⚠ 这排按钮平时 <c>Opacity=0</c>、悬停卡片才显现（Rider 的做法），
    /// 显隐由主题管，模板里不用再管。
    /// </para>
    /// </remarks>
    public static readonly AttachedProperty<IDataTemplate?> CardActionsTemplateProperty =
        AvaloniaProperty.RegisterAttached<Control, Control, IDataTemplate?>(
            "CardActionsTemplate", inherits: true);

    /// <summary>读 <see cref="CardActionsTemplateProperty"/>。</summary>
    public static IDataTemplate? GetCardActionsTemplate(Control element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(CardActionsTemplateProperty);
    }

    /// <summary>写 <see cref="CardActionsTemplateProperty"/>。</summary>
    public static void SetCardActionsTemplate(Control element, IDataTemplate? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(CardActionsTemplateProperty, value);
    }
}
