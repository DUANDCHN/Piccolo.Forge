using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Piccolo.Forge.Avalonia;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 主题入口 <c>Themes/ForgeTheme.axaml</c> 把**每一个**控件主题都挂上了（REQ-067）。
/// </summary>
/// <remarks>
/// ★ 这条守的是"一个控件一份 axaml"之后唯一新增的失手方式：
/// <b>加了文件、忘了在入口里 <c>ResourceInclude</c></b>。
/// 后果与漏一个颜色键同类 —— **不报错**，那个控件只是没有 <c>ControlTheme</c>、
/// 什么都不画；而它多半藏在某个不常开的面板里，人眼比对也未必翻得到。
/// <para>
/// 判据取的是"能不能按类型找到 <c>ControlTheme</c>"，即 Avalonia 自己找模板时走的那条路
/// （按控件的**实际类型**作键），不是"文件在不在磁盘上"。
/// </para>
/// </remarks>
public class ForgeThemeEntryTests
{
    /// <summary>十个控件，与需求里那张现状表一一对应。</summary>
    private static readonly Type[] Themed =
    [
        typeof(ForgeStripeButton),
        typeof(ForgeStripeSeparator),
        typeof(ForgeMoreButton),
        typeof(ForgeHeaderButton),
        typeof(ForgeContentTab),
        typeof(ForgeEditorTab),
        typeof(ForgeToolWindowToolbar),
        typeof(ForgeToolWindowHeader),
        typeof(ForgeEditorSplitter),
        typeof(ForgeDragOverlay),
    ];

    [AvaloniaFact]
    public void 十个控件的主题都挂进了入口()
    {
        var missing = new List<string>();

        foreach (var type in Themed)
        {
            if (!Application.Current!.TryFindResource(type, out var found) || found is not ControlTheme)
            {
                missing.Add(type.Name);
            }
        }

        Assert.Empty(missing);
    }
}
