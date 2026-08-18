using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// ★ 每一套主题都必须是**完整的一套**：全部控件有 ControlTheme、全部颜色键有值（REQ-068）。★
/// </summary>
/// <remarks>
/// <b>这是 REQ-068 的灵魂，比拆分本身更重要。</b>立项动机就是一个反复发作的缺陷类
/// ——「改一族、漏一族，而且漏了没有任何提示」（e82594b 的内容 tab 只换色没换形状、
/// b9dc25a 的落点投影两族画同一个块、更早那批经典档整批缺失的底色与分隔线）。
/// 拆成两套之后这个风险不减反增：结构要双写，谁也记不住第二套。
/// <para>
/// 所以判据必须是**机器可查**的，而且清单不能是手工维护的第二份真相（R3）：
/// 控件从**反射扫本程序集**来，故意不要主题的在控件自己头上写
/// <see cref="ForgeThemelessAttribute"/> 说明理由；颜色键从
/// <see cref="ForgeThemeDictionaryTests.Entries"/> 来（那份本来就与 <see cref="ForgePalette"/> 同源）。
/// </para>
/// <para>
/// <b>反证记录</b>（这套仓的惯例，见 REQ-067 的守护测试）：临时删掉
/// <c>Themes/Islands/ForgeMoreButton.axaml</c> 的 include，第一条当场红并点名
/// <c>ForgeMoreButton</c>；临时删掉 <c>Themes/Islands/Colors.axaml</c> 里的
/// <c>Forge.Islands.StripeBorder</c>，第二条当场红并点名那个键。
/// </para>
/// </remarks>
public class ForgeThemeCompletenessTests
{
    public static TheoryData<ForgeThemeFamily> Families => new(Enum.GetValues<ForgeThemeFamily>());

    public static TheoryData<ForgeThemeFamily, bool> FamiliesAndVariants
    {
        get
        {
            var data = new TheoryData<ForgeThemeFamily, bool>();
            foreach (var family in Enum.GetValues<ForgeThemeFamily>())
            {
                data.Add(family, false);
                data.Add(family, true);
            }

            return data;
        }
    }

    /// <summary>每套主题都为**每个**控件备了一份 ControlTheme。</summary>
    /// <remarks>
    /// 判据取的是"能不能按类型找到 ControlTheme"，即 Avalonia 自己找模板时走的那条路
    /// （按控件的**实际类型**作键），不是"文件在不在磁盘上"——漏 <c>ResourceInclude</c>
    /// 与漏文件的症状一样：不报错，那个控件只是什么都不画。
    /// </remarks>
    [AvaloniaTheory]
    [MemberData(nameof(Families))]
    public void 每套主题都给每个控件备了ControlTheme(ForgeThemeFamily family)
    {
        using var theme = ForgeThemeScope.Use(family);

        var missing = ThemedControls()
            .Where(type => !Application.Current!.TryFindResource(type, out var found) || found is not ControlTheme)
            .Select(type => type.Name)
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>每套主题都为**每个**颜色键给了值，明暗两个变体各一份。</summary>
    [AvaloniaTheory]
    [MemberData(nameof(FamiliesAndVariants))]
    public void 每套主题都给每个颜色键备了值(ForgeThemeFamily family, bool dark)
    {
        using var theme = ForgeThemeScope.Use(family);

        var window = new Window
        {
            Width = 200,
            Height = 100,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        var probe = new Border();
        window.Content = probe;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var missing = ForgeThemeDictionaryTests.Entries
            .Select(entry => $"Forge.{family}.{entry.Suffix}")
            .Where(key => !probe.TryFindResource(key, probe.ActualThemeVariant, out var value) || value is not Color)
            .ToList();

        window.Close();

        Assert.Empty(missing);
    }

    /// <summary>
    /// 装了一族之后，**另一族的键不该还在**。
    /// </summary>
    /// <remarks>
    /// 这条守的是"两套主题真的各自成套"：只要还有谁在自己的单元里引了别族的键
    /// （拆分时最容易发生的搬运事故：整份复制过来忘了改前缀），这里就红。
    /// 它同时是"一个单元只有一族"这句话的可执行定义。
    /// </remarks>
    [AvaloniaTheory]
    [MemberData(nameof(Families))]
    public void 装了一族就不该看见另一族的键(ForgeThemeFamily family)
    {
        using var theme = ForgeThemeScope.Use(family);

        var window = new Window { Width = 200, Height = 100, RequestedThemeVariant = ThemeVariant.Dark };
        var probe = new Border();
        window.Content = probe;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var other = Enum.GetValues<ForgeThemeFamily>().Single(x => x != family);
        var leaked = ForgeThemeDictionaryTests.Entries
            .Select(entry => $"Forge.{other}.{entry.Suffix}")
            .Where(key => probe.TryFindResource(key, probe.ActualThemeVariant, out _))
            .ToList();

        window.Close();

        Assert.Empty(leaked);
    }

    /// <summary>
    /// 不给主题的控件必须**自己写明理由**。
    /// </summary>
    /// <remarks>
    /// 反过来的一条：<see cref="ForgeThemelessAttribute"/> 只该戴在真没有主题的控件上。
    /// 戴错了（有主题却声明没有）说明谁把它当成了消掉报红的开关 —— 那就把守护绕过去了。
    /// </remarks>
    [AvaloniaFact]
    public void 声明没有主题的控件确实没有主题()
    {
        using var theme = ForgeThemeScope.Use(ForgeThemeScope.Default);

        var lying = Controls()
            .Where(type => type.GetCustomAttribute<ForgeThemelessAttribute>() is not null)
            .Where(type => Application.Current!.TryFindResource(type, out var found) && found is ControlTheme)
            .Select(type => type.Name)
            .ToList();

        Assert.Empty(lying);
    }

    /// <summary>
    /// ★ 宿主还没设外观时，控件**跟随装了哪族**，不会因为"默认那份是经典"而炸。★
    /// </summary>
    /// <remarks>
    /// 回归：真机启动即崩（配置是 Islands，主窗口 XAML 里的 <see cref="ForgeControl"/> 一挂树就抛
    /// "主题不一致"）。成因是控件由 XAML 建、挂树那一下外观还是 <see cref="ForgeAppearance.Default"/>
    /// —— 宿主那份在 code-behind 里晚一步才设，而那个瞬间的 <c>Family</c> 不是任何人的主张。
    /// <para>
    /// 判据两条：**不抛**，且配色取的是**装进去那一族**（不是默认那族）——
    /// 只做到不抛的话，首帧会是"模板 Islands、几何取色 expUI"的半旧半新。
    /// 一致性断言仍然管着宿主**真给过**外观的那种不一致，见 <see cref="ForgeThemeManager.AssertConsistent"/>。
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void 宿主没设外观时跟随装了哪族()
    {
        using var theme = ForgeThemeScope.Use(ForgeThemeFamily.Islands);

        var frame = new ForgeControl();
        var window = new Window { Width = 400, Height = 300, Content = frame, RequestedThemeVariant = ThemeVariant.Dark };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Same(ForgePalette.IslandsDark, frame.Palette);

        window.Close();
    }

    /// <summary>本程序集里**该有**主题的控件 —— 反射扫出来的，不是手工清单。</summary>
    private static IEnumerable<Type> ThemedControls()
        => Controls().Where(type => type.GetCustomAttribute<ForgeThemelessAttribute>() is null);

    /// <summary>本程序集里全部**能实例化的** <see cref="Control"/> 子类。</summary>
    private static IEnumerable<Type> Controls()
        => typeof(ForgeControl).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsPublic: true })
            .Where(typeof(Control).IsAssignableFrom)
            .OrderBy(type => type.Name);
}
