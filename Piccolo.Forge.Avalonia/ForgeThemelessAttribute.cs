using System;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 声明这个控件**故意没有 ControlTheme**，并写明为什么（REQ-068）。
/// </summary>
/// <remarks>
/// <b>它存在的唯一理由是让完整性守护有单一真相。</b>
/// <c>ForgeThemeCompletenessTests</c> 反射扫本程序集的全部 <c>Control</c> 子类，
/// 要求每一个在**每套主题**里都有一份 ControlTheme —— 治的是 REQ-068 立项动机那条
/// 「改一族漏一族且无提示」。可确实有些控件不该有主题（纯布局面板、图标槽、浮动窗口……），
/// 名单要么写在测试里（那就成了第二份手工清单，REQ-068 R3 点名要避免的），
/// 要么就写在控件自己头上 —— 选后者。
/// <para>
/// 于是新增一个控件时只有两条路：给两套主题各加一份，或者在这里写下不加的理由。
/// 忘了做，测试当场红。
/// </para>
/// </remarks>
/// <param name="reason">为什么它不需要主题 —— 写给下一个看见测试报红的人。</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ForgeThemelessAttribute(string reason) : Attribute
{
    /// <summary>不给主题的理由。</summary>
    public string Reason { get; } = reason;
}
