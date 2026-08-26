using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 输入框（文本框 / 数字框 / 下拉框）的**边框状态**：常态中性、悬停与聚焦都变主题色，粗细自始至终不变。
/// </summary>
/// <remarks>
/// <b>由来（宿主实机反馈，2026-08-26）。</b>两件事：
/// <list type="number">
/// <item>下拉框的 <c>:pointerover</c> 样式一直把 <c>BorderBrush</c> 设成 <c>Forge.Input.Border</c>
/// —— 与常态**同值**，等于什么都没做，"鼠标移上去像没反应"。死样式不报错。</item>
/// <item>聚焦态原先照上游画成「框外一圈 2px 的 focusColor 光晕 + 内边框转成与底同色」，
/// 观感上就是"边框加粗了"，而框里文字位置又没动，别扭。改成边框自己变主题色、外环不再点亮。</item>
/// </list>
/// <para>
/// ⚠ <b>粗细不变这半同样要钉。</b><c>Border</c> 改 <c>BorderThickness</c> 会挤压内容区，
/// 鼠标移上去框里的文字会挪 1px，看着就是抖。这条断言拦住"顺手把状态也加粗"。
/// </para>
/// <para>
/// ⚠ <c>PART_FocusRing</c> 那个元素**保留**（它占 2px 布局），只是永远透明；删掉会让每个输入框
/// 外沿缩 2px，全应用的间距跟着变。所以判据是"环是透明的"，不是"环没了"。
/// </para>
/// </remarks>
public class InputStateTests
{
    [AvaloniaTheory]
    [InlineData(ForgeThemeFamily.ExpUi)]
    [InlineData(ForgeThemeFamily.Islands)]
    public void 下拉框悬停时边框变主题色且粗细不变(ForgeThemeFamily family)
    {
        using var theme = ForgeThemeScope.Use(family);
        var window = Host(out var text, out var numeric, out var combo);

        var root = Part(combo, "PART_Root");
        var idle = root.BorderThickness;
        Assert.Equal(Resource(combo, "Forge.Input.Border"), ColorOf(root.BorderBrush));

        window.MouseMove(combo.TranslatePoint(Center(combo), window)!.Value);
        Relayout(window);

        Assert.True(combo.IsPointerOver);
        Assert.Equal(Resource(combo, "Forge.Input.HoverBorder"), ColorOf(root.BorderBrush));
        Assert.NotEqual(Resource(combo, "Forge.Input.Border"), ColorOf(root.BorderBrush));
        // 悬停色就是主题色本身 —— 与焦点环同源，宿主换种子色时一起换。
        Assert.Equal(Resource(combo, "Forge.Focus.Ring"), ColorOf(root.BorderBrush));
        Assert.Equal(idle, root.BorderThickness);

        window.Close();
    }

    /// <summary>文本框与数字框聚焦时，边框变主题色，外环仍透明，粗细不变。</summary>
    /// <remarks>
    /// 数字框的边框住在它内层的 <c>ButtonSpinner</c> 上（<c>NumericUpDown</c> 自己不画框），
    /// 焦点则落在更内层的 <c>TextBox</c> 上 —— 靠 <c>:focus-within</c> 一路传上去。
    /// 所以这条同时钉住"焦点传导没断"：断了的话边框不变色，而且不报错。
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(ForgeThemeFamily.ExpUi)]
    [InlineData(ForgeThemeFamily.Islands)]
    public void 输入框聚焦时边框变主题色且不点亮外环(ForgeThemeFamily family)
    {
        using var theme = ForgeThemeScope.Use(family);
        var window = Host(out var text, out var numeric, out var combo);

        var accent = Resource(text, "Forge.Focus.Ring");
        // 防空断言：取不到键时 Resource 返回 null，下面拿 null 比 null 会**假绿**。
        Assert.NotNull(accent);

        foreach (var (owner, focusTarget) in new (Control Owner, Control Target)[]
                 {
                     (text, text),
                     // 数字框：边框在 ButtonSpinner 上，焦点在它内层的 TextBox 上
                     (numeric.GetVisualDescendants().OfType<ButtonSpinner>().Single(),
                      numeric.GetVisualDescendants().OfType<TextBox>().Single()),
                 })
        {
            var root = Part(owner, "PART_Root");
            var ring = Part(owner, "PART_FocusRing");
            var idle = root.BorderThickness;

            focusTarget.Focus(NavigationMethod.Tab);
            Relayout(window);

            Assert.Equal(accent, ColorOf(root.BorderBrush));
            Assert.Equal(Colors.Transparent, ColorOf(ring.BorderBrush));
            Assert.Equal(idle, root.BorderThickness);
        }

        window.Close();
    }

    /// <summary>聚焦色与悬停色都由主题色种子驱动 —— 宿主换主题色时两者一起换。</summary>
    /// <remarks>
    /// 漏掉任一个键的症状是"整屏都换了颜色，只有输入框那两态还是库里那支蓝"。
    /// 具体派生归 <see cref="ForgeAccentTests"/> 验，这里只钉"它俩取的就是主题色"。
    /// </remarks>
    [AvaloniaFact]
    public void 悬停色与聚焦色都是主题色本身()
    {
        using var theme = ForgeThemeScope.Use(ForgeThemeScope.Default);
        var window = Host(out var text, out _, out _);

        var accent = Resource(text, "Forge.Focus.Ring");
        Assert.NotNull(accent);
        Assert.Equal(accent, Resource(text, "Forge.Input.HoverBorder"));
        Assert.Equal(accent, Resource(text, "Forge.Input.FocusedBorder"));

        window.Close();
    }

    /// <summary>宿主在控件上写死 <c>Height</c>，**画边框那层就是那个高度**。</summary>
    /// <remarks>
    /// <b>反面（本条的由来，2026-08-26 宿主实测）。</b>模板最外层套着 <c>PART_FocusRing</c>，
    /// 原先厚度 2、常态透明但**始终占位**。于是设置窗里给每个控件写 <c>Height="30"</c>，
    /// 量出来是：控件 30 / <c>PART_Root</c> 26 / 内容 24 —— 看得见的框比写的矮 4px，
    /// 而且哪层吃掉的从界面上看不出来。宿主换了三种写法都没用，根因在库里。
    /// <para>
    /// 聚焦既然已经改画在 <c>PART_Root</c> 自己的边框上，那圈环就是纯占位，厚度归 0。
    /// 这条钉住的是**契约**：写 Height=N 就得到 N，宿主不必去猜模板套了几层。
    /// </para>
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(ForgeThemeFamily.ExpUi)]
    [InlineData(ForgeThemeFamily.Islands)]
    public void 写死高度时画边框那层就是那个高度(ForgeThemeFamily family)
    {
        using var theme = ForgeThemeScope.Use(family);

        var text = new TextBox { Width = 160, Height = 30, Text = "x" };
        var numeric = new NumericUpDown { Width = 160, Height = 30, Value = 1 };
        var combo = new ComboBox { Width = 160, Height = 30, ItemsSource = new[] { "x" }, SelectedIndex = 0 };
        var button = new Button { Width = 160, Height = 30, Content = "x" };
        var window = new Window
        {
            Width = 240,
            Height = 320,
            Content = new StackPanel { Spacing = 8, Children = { text, numeric, combo, button } },
        };
        window.Show();
        Relayout(window);

        foreach (var c in new Control[] { text, numeric, combo, button })
        {
            Assert.Equal(30, c.Bounds.Height);
            Assert.Equal(30, Part(c, "PART_Root").Bounds.Height);
        }

        window.Close();
    }

    private static Window Host(out TextBox text, out NumericUpDown numeric, out ComboBox combo)
    {
        text = new TextBox { Width = 110, Text = "abc" };
        numeric = new NumericUpDown { Width = 110, Value = 100 };
        combo = new ComboBox { Width = 110, ItemsSource = new[] { "5" }, SelectedIndex = 0 };
        var window = new Window
        {
            Width = 220,
            Height = 220,
            Content = new StackPanel { Spacing = 12, Children = { text, numeric, combo } },
        };

        window.Show();
        Relayout(window);
        return window;
    }

    /// <remarks>
    /// 用 <c>First</c> 而不是 <c>Single</c>：数字框里嵌着一个 TextBox，它自己也有一份同名部件。
    /// 深度优先下本控件自己的那个排在最前。
    /// </remarks>
    private static Border Part(Control owner, string name)
        => owner.GetVisualDescendants().OfType<Border>().First(x => x.Name == name);

    private static Point Center(Control c) => new(c.Bounds.Width / 2, c.Bounds.Height / 2);

    private static Color? ColorOf(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    /// <remarks>
    /// ⚠ 必须带上 <c>ActualThemeVariant</c>：颜色键住在 <c>ThemeDictionaries</c> 里，
    /// 不带变体取到的是"没有变体"那一档，结果是 <c>null</c>（不报错）。
    /// </remarks>
    private static Color? Resource(Control probe, string key)
        => probe.TryFindResource(key, probe.ActualThemeVariant, out var value)
            ? (value as ISolidColorBrush)?.Color
            : null;

    private static void Relayout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.GetVisualDescendants().OfType<Layoutable>().ToList().ForEach(x => x.InvalidateMeasure());
        Dispatcher.UIThread.RunJobs();
    }
}
