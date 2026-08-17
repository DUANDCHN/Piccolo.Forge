using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Linq;
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
    /// <summary>十个控件（与需求里那张现状表一一对应），外加两个"岛的宿主"。</summary>
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

        // 两个"岛的宿主"：不是那 10 个控件之一，但同样有一份只喂画刷的 ControlTheme
        // —— 漏挂它们的症状是岛那圈描边回落到配色里的默认值（REQ-067 收尾项）
        typeof(ForgeControl),
        typeof(ForgeEditorControl),
    ];

    [AvaloniaFact]
    public void 每个控件的主题都挂进了入口()
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

    /// <summary>
    /// 两个标签都不许落回 Avalonia 自带的那份模板。
    /// </summary>
    /// <remarks>
    /// ★ 这条是 <c>d7ee5f1</c> 那个回归的防回潮用例。
    /// <c>TabStripItem</c> 是 <c>ContentControl</c>，Avalonia 兜了一份带
    /// <c>PART_ContentPresenter</c> 的模板；而标签的容器由 <c>TabStrip</c> 建，
    /// <c>Content</c> 被喂成**视图模型对象本身** —— 一旦落回兜底模板，
    /// 它就把对象 <c>ToString()</c> 出来画在标签上
    /// （实机症状：标签行顶上多出一串 <c>Piccolo.Forge.Mvvm.EditorTabViewModel</c>）。
    /// <para>
    /// 成因是"清掉构造里的 <c>Template</c> 却没在 <c>ControlTheme</c> 里补一份"。
    /// 由此的规矩：**删构造里的 Template，必须同时补一份到主题里，哪怕是空的。**
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void 标签不会落回Avalonia自带的模板()
    {
        AssertNoFallbackTemplate(new ForgeEditorTab { Title = "文档" });
        AssertNoFallbackTemplate(new ForgeContentTab { Title = "内容" });
    }

    private static void AssertNoFallbackTemplate(ContentControl tab)
    {
        // 喂一个"不是字符串"的 Content —— 与 TabStrip 建容器时喂的东西同类
        tab.Content = new NotForDisplay();

        var window = new Window
        {
            Width = 300,
            Height = 60,
            Content = tab,
            RequestedThemeVariant = ThemeVariant.Dark,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(
            tab.GetVisualDescendants().OfType<ContentPresenter>(),
            x => x.Name == "PART_ContentPresenter");
        Assert.DoesNotContain(
            tab.GetVisualDescendants().OfType<TextBlock>(),
            x => x.Text == NotForDisplay.Marker);

        window.Close();
    }

    /// <summary>专门用来暴露"兜底模板把 Content 画出来了"的替身。</summary>
    private sealed class NotForDisplay
    {
        public const string Marker = "★这串不该出现在标签上★";

        public override string ToString() => Marker;
    }
}
