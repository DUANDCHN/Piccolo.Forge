using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// REQ-068 的 R1 探路用例：<b>运行时换掉 Application.Styles 里的主题单元之后，
/// 已经挂在树上的控件会不会重新解析 ControlTheme、重做模板。</b>
/// </summary>
/// <remarks>
/// 这条结论决定 REQ-068 的方案形态：行，就能"一族一套完整主题、切换即换单元"；
/// 不行，就只能退回顶层 theme-class 之类的选择器写法（需求文档 R1 的备选 a/b）。
/// 与 <see cref="ThemeResourceSpikeTests"/> 同性质 —— 测的是 Avalonia 的行为，不是 Forge 的。
/// </remarks>
public class ThemeSwapSpikeTests
{
    private const string PartA = "PART_UnitA";
    private const string PartB = "PART_UnitB";

    /// <summary>★ 结论：**不会**。这是 REQ-068 方案形态的分水岭。★</summary>
    /// <remarks>
    /// 已挂树的控件把 ControlTheme 认死了，换掉单元不重取 —— 所以"切主题即时生效"这条做不到，
    /// 需求据此改成**切换只写配置、重启生效**（用户 2026-08-18 拍板）。
    /// </remarks>
    [AvaloniaFact]
    public void 换掉Styles单元之后已挂树的控件不会重做模板()
    {
        var unitA = NewUnit(PartA);
        var unitB = NewUnit(PartB);

        var app = Application.Current!;
        app.Styles.Add(unitA);

        var probe = new SpikeControl();
        var window = new Window { Width = 200, Height = 100, Content = probe };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PartA, NameOfTemplatedChild(probe));

        // ★ 单一入口要做的那一步：原子地把单元 A 换成单元 B
        app.Styles.Remove(unitA);
        app.Styles.Add(unitB);
        Dispatcher.UIThread.RunJobs();

        var after = NameOfTemplatedChild(probe);

        app.Styles.Remove(unitB);
        window.Close();

        Assert.Equal(PartA, after);
    }

    /// <summary>摘挂之后会重取 —— 备选路（切换时摘挂外壳）成立的依据，本次未采用。</summary>
    [AvaloniaFact]
    public void 摘挂之后会不会重取()
    {
        var unitA = NewUnit(PartA);
        var unitB = NewUnit(PartB);
        var app = Application.Current!;
        app.Styles.Add(unitA);

        var probe = new SpikeControl();
        var host = new Decorator { Child = probe };
        var window = new Window { Width = 200, Height = 100, Content = host };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(PartA, NameOfTemplatedChild(probe));

        app.Styles.Remove(unitA);
        app.Styles.Add(unitB);
        host.Child = null;
        Dispatcher.UIThread.RunJobs();
        host.Child = probe;
        Dispatcher.UIThread.RunJobs();

        var after = NameOfTemplatedChild(probe);
        app.Styles.Remove(unitB);
        window.Close();

        Assert.Equal(PartB, after);
    }

    /// <summary>换单元之后**新建**的控件：取的是新单元还是旧单元。</summary>
    [AvaloniaFact]
    public void 换单元之后新建的控件取新单元()
    {
        var unitA = NewUnit(PartA);
        var unitB = NewUnit(PartB);
        var app = Application.Current!;
        app.Styles.Add(unitA);

        var window = new Window { Width = 200, Height = 100, Content = new SpikeControl() };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        app.Styles.Remove(unitA);
        app.Styles.Add(unitB);
        Dispatcher.UIThread.RunJobs();

        var fresh = new SpikeControl();
        window.Content = fresh;
        Dispatcher.UIThread.RunJobs();

        var after = NameOfTemplatedChild(fresh);
        app.Styles.Remove(unitB);
        window.Close();

        Assert.Equal(PartB, after);
    }

    /// <summary>★ 结论：翻明暗变体**也不会**顺带重取 ControlTheme。★</summary>
    /// <remarks>
    /// 这条堵死了"换完单元再翻一下变体逼它重取"这个看似顺手的偏方。
    /// </remarks>
    [AvaloniaFact]
    public void 翻变体也不会触发重取()
    {
        var unitA = NewUnit(PartA);
        var unitB = NewUnit(PartB);
        var app = Application.Current!;
        app.Styles.Add(unitA);

        var probe = new SpikeControl();
        var window = new Window
        {
            Width = 200,
            Height = 100,
            Content = probe,
            RequestedThemeVariant = ThemeVariant.Dark,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(PartA, NameOfTemplatedChild(probe));

        app.Styles.Remove(unitA);
        app.Styles.Add(unitB);
        window.RequestedThemeVariant = ThemeVariant.Light;
        Dispatcher.UIThread.RunJobs();

        var after = NameOfTemplatedChild(probe);
        app.Styles.Remove(unitB);
        window.Close();

        Assert.Equal(PartA, after);
    }

    /// <summary>
    /// 摘挂**外层一个容器**，深处的控件会不会跟着重取；期间"活控件"是不是同一个实例、状态还在。
    /// </summary>
    /// <remarks>对位 R1 备选 b：面板内容是宿主的活控件（相机画面在里面），不能重建。</remarks>
    [AvaloniaFact]
    public void 摘挂外层容器深处也重取且活控件保活()
    {
        var unitA = NewUnit(PartA);
        var unitB = NewUnit(PartB);
        var app = Application.Current!;
        app.Styles.Add(unitA);

        var deep = new SpikeControl();
        var live = new TextBox { Text = "活控件的状态" };
        var root = new StackPanel { Children = { deep, new Decorator { Child = live } } };
        var host = new Decorator { Child = root };
        var window = new Window { Width = 200, Height = 200, Content = host };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(PartA, NameOfTemplatedChild(deep));

        app.Styles.Remove(unitA);
        app.Styles.Add(unitB);
        host.Child = null;
        host.Child = root;
        Dispatcher.UIThread.RunJobs();

        var after = NameOfTemplatedChild(deep);
        var sameLive = ReferenceEquals(live, ((Decorator)root.Children[1]).Child);
        var text = live.Text;

        app.Styles.Remove(unitB);
        window.Close();

        Assert.Equal(PartB, after);
        Assert.True(sameLive);
        Assert.Equal("活控件的状态", text);
    }

    /// <summary>显式给控件设 <c>Theme</c>：能不能直接换掉模板（不摘挂）。</summary>
    [AvaloniaFact]
    public void 显式设Theme能换掉模板()
    {
        var unitA = NewUnit(PartA);
        var app = Application.Current!;
        app.Styles.Add(unitA);

        var probe = new SpikeControl();
        var window = new Window { Width = 200, Height = 100, Content = probe };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(PartA, NameOfTemplatedChild(probe));

        probe.Theme = (ControlTheme)NewUnit(PartB).Resources[typeof(SpikeControl)]!;
        Dispatcher.UIThread.RunJobs();
        var after = NameOfTemplatedChild(probe);

        app.Styles.Remove(unitA);
        window.Close();

        Assert.Equal(PartB, after);
    }

    private static string? NameOfTemplatedChild(SpikeControl probe)
        => probe.GetVisualDescendants().OfType<Border>().Select(x => x.Name).FirstOrDefault();

    /// <summary>一个"主题单元"：只含本控件的一份 ControlTheme，对位拆分后的 Theme.axaml。</summary>
    private static Styles NewUnit(string partName)
    {
        var theme = new ControlTheme(typeof(SpikeControl));
        theme.Setters.Add(new Setter(
            TemplatedControl.TemplateProperty,
            new FuncControlTemplate<SpikeControl>((_, scope) =>
                new Border { Name = partName }.RegisterInNameScope(scope))));

        var unit = new Styles();
        unit.Resources[typeof(SpikeControl)] = theme;

        return unit;
    }

    private sealed class SpikeControl : TemplatedControl;
}
