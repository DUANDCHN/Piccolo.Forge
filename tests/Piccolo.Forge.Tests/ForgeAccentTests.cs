using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 主题色种子：宿主给明暗各一支色，色板里那一批跟着强调色走的键全跟着换。
/// </summary>
/// <remarks>
/// <b>由来。</b>在种子键之前，宿主换主题色要逐键覆盖 —— 控件 9 个 + 外壳每族 8 个，
/// 两族明暗四档共 <b>37 处手工色值</b>。实测踩到的正是它的失败模式：只盖了控件那半，
/// 面板里的选中条变了、**侧边栏条带按钮还是原来的蓝**，而且不报错。
/// <para>
/// 断言分三组：<b>盖到了</b>（控件与外壳两半都要）、<b>没盖多</b>（中性色、只装一层的宿主）、
/// <b>反复设不漂移</b>（派生不能拿上一次的派生结果再派生）。
/// </para>
/// <para>
/// ⚠ 本类**直接问主题实例要资源**，不装进 <see cref="Application"/>。
/// 装进去要经 <see cref="ForgeThemeManager"/>，而它有意"同族不换 Styles"（换了会让已挂树的控件
/// 重新套模板）—— 那条路径归 <see cref="同族重复Apply会把种子色接过去"/> 一条专门验，
/// 其余断言不该被它牵着走。
/// </para>
/// </remarks>
public class ForgeAccentTests
{
    private static readonly Color Blue = Color.Parse("#0064FA");
    private static readonly Color Orange = Color.Parse("#B8680F");

    /// <summary>不给种子色时一个键都不动 —— 本属性存在之前与之后，默认外观必须逐字节一样。</summary>
    [AvaloniaTheory]
    [InlineData(ForgeThemeFamily.ExpUi, "#FF3574F0")]
    [InlineData(ForgeThemeFamily.Islands, "#FF3871E1")]
    public void 不给种子色时色板原样不动(ForgeThemeFamily family, string stockAccent)
    {
        var theme = new ForgeTheme { Family = family };

        Assert.Equal(Color.Parse(stockAccent), BrushColor(theme, "Forge.Button.Accent.Background", ThemeVariant.Light));
    }

    /// <summary>实心填充档取种子原值 —— 控件与外壳**两半都要**（只盖一半正是本需求的由来）。</summary>
    [AvaloniaTheory]
    [InlineData(ForgeThemeFamily.ExpUi)]
    [InlineData(ForgeThemeFamily.Islands)]
    public void 实心填充档明暗各取自己那支种子色(ForgeThemeFamily family)
    {
        var theme = Seeded(family);

        foreach (var key in new[]
                 {
                     "Forge.Button.Accent.Background", "Forge.Focus.Ring",
                     "Forge.Check.SelectedBackground", "Forge.Check.SelectedBorder", "Forge.Progress.Bar",
                 })
        {
            Assert.Equal(Blue, BrushColor(theme, key, ThemeVariant.Light));
            Assert.Equal(Orange, BrushColor(theme, key, ThemeVariant.Dark));
        }

        // 外壳那半（键带族前缀、类型是 Color 而不是 Brush）—— 侧边栏条带按钮的选中底就是它。
        foreach (var key in new[] { "ButtonSelectedBackground", "HeaderTabUnderline" })
        {
            Assert.Equal(Blue, ShellColor(theme, family, key, ThemeVariant.Light));
            Assert.Equal(Orange, ShellColor(theme, family, key, ThemeVariant.Dark));
        }
    }

    /// <summary>派生档保住色板原本的明度、只换色相，且 <b>alpha 原样留着</b>。</summary>
    /// <remarks>
    /// 期望值不是从实现里抄的，是**另一份实现**（改动时用 Python 按同一条规则算的）给的，
    /// 属于交叉验算而不是自证。挑的键覆盖三种情形：不透明的浅底、比强调色浅一档的指示线、
    /// 带 alpha 的拖放蒙版。
    /// </remarks>
    [AvaloniaFact]
    public void 派生档保明度换色相且不动alpha()
    {
        var theme = Seeded(ForgeThemeFamily.ExpUi);

        Assert.Equal(Color.Parse("#FFD4E5FF"), BrushColor(theme, "Forge.Item.SelectedBackground", ThemeVariant.Light));
        Assert.Equal(Color.Parse("#FF6E502E"), BrushColor(theme, "Forge.Item.SelectedBackground", ThemeVariant.Dark));

        Assert.Equal(Color.Parse("#FF66A3FF"), ShellColor(theme, ForgeThemeFamily.ExpUi, "StripeSeparatorDragAndDrop", ThemeVariant.Light));
        // alpha 段 0x4D / 0x99 必须原样：那是叠加强度，不是颜色。
        Assert.Equal(Color.Parse("#4D99C2FF"), ShellColor(theme, ForgeThemeFamily.ExpUi, "DragAreaBackground", ThemeVariant.Light));
        Assert.Equal(Color.Parse("#998E6436"), ShellColor(theme, ForgeThemeFamily.ExpUi, "ButtonDropBackground", ThemeVariant.Dark));
    }

    /// <summary>名单里的键在某一族/某一档下本来就是中性的，不参与派生。</summary>
    /// <remarks>
    /// 经典档的编辑区选中标签底是 <c>#FFFFFF</c> / <c>#1E1F22</c> —— 它不是主题色。
    /// 少了中性判据的症状是"暗色下标签底变成一块偏暖的灰"，不报错、只是脏。
    /// </remarks>
    [AvaloniaFact]
    public void 中性色不参与派生()
    {
        var theme = Seeded(ForgeThemeFamily.ExpUi);

        Assert.Equal(Colors.White, ShellColor(theme, ForgeThemeFamily.ExpUi, "EditorTabSelectedBackground", ThemeVariant.Light));
        Assert.Equal(Color.Parse("#FF1E1F22"), ShellColor(theme, ForgeThemeFamily.ExpUi, "EditorTabSelectedBackground", ThemeVariant.Dark));
    }

    /// <summary>Islands 不画标签下划线（那支是全透明），归到实心填充档也**不能**凭空画出一条线来。</summary>
    /// <remarks>
    /// 这条钉的是"实心档保留原值 alpha"这个决定。写成 <c>Color.FromRgb(种子)</c> 就会把 alpha 变成
    /// 0xFF，于是 Islands 的每个标签底下多一条橙线 —— 没人会想到去看这个键。
    /// </remarks>
    [AvaloniaFact]
    public void 原本全透明的键换了种子色仍然不可见()
    {
        var theme = Seeded(ForgeThemeFamily.Islands);

        Assert.Equal(0, BrushColor(theme, "Forge.Tab.Underline", ThemeVariant.Light).A);
        Assert.Equal(0, BrushColor(theme, "Forge.Tab.Underline", ThemeVariant.Dark).A);
    }

    /// <summary>
    /// Islands 的勾选框选中色是 <c>#3574F0</c>（经典档那支蓝），与它自己的强调色 <c>#3871E1</c> 差一点。
    /// 档位靠**显式名单**而不是"原值等于强调色"，所以它照样被当成实心填充。
    /// </summary>
    /// <remarks>反证记录：自动判档的那版实现在这里算出 <c>#267DFF</c>（过饱和的怪蓝），是本条的由来。</remarks>
    [AvaloniaFact]
    public void 与族强调色不完全相等的实心档也照样取种子色()
    {
        var theme = Seeded(ForgeThemeFamily.Islands);

        Assert.Equal(Blue, BrushColor(theme, "Forge.Check.SelectedBackground", ThemeVariant.Light));
        Assert.Equal(Orange, BrushColor(theme, "Forge.Check.SelectedBorder", ThemeVariant.Dark));
    }

    /// <summary>反复改种子色**不漂移** —— 派生必须永远从色板原值算，不能拿上一次的派生结果再派生一次。</summary>
    /// <remarks>
    /// 这是本实现最容易写错的一处：派生字典自己也在 <c>Resources.MergedDictionaries</c> 里，
    /// 读原值时若不绕开它，第二次重算读到的就是第一次的产物。症状是"设置页里来回换几次主题色，
    /// 颜色越换越淡"，且每一步看着都合理。
    /// </remarks>
    [AvaloniaFact]
    public void 反复改种子色不会逐次漂移()
    {
        var theme = Seeded(ForgeThemeFamily.ExpUi);
        var first = BrushColor(theme, "Forge.Item.SelectedBackground", ThemeVariant.Light);

        theme.AccentLight = Color.Parse("#12B886");   // 换成绿
        theme.AccentLight = Color.Parse("#E64980");   // 再换成粉
        theme.AccentLight = Blue;                     // 换回来

        Assert.Equal(first, BrushColor(theme, "Forge.Item.SelectedBackground", ThemeVariant.Light));
    }

    /// <summary>换族之后按**新族的**原值重算 —— 两族的派生档原值不同，不重算就是拿旧族的明度阶梯。</summary>
    [AvaloniaFact]
    public void 换族之后按新族的原值重算()
    {
        var theme = Seeded(ForgeThemeFamily.ExpUi);
        var expUi = BrushColor(theme, "Forge.Item.SelectedBackground", ThemeVariant.Light);

        theme.Family = ForgeThemeFamily.Islands;
        var islands = BrushColor(theme, "Forge.Item.SelectedBackground", ThemeVariant.Light);

        // 两族原值分别是 #D4E2FF / #D0DFFE，明度不同，派生结果也就不该相同。
        Assert.NotEqual(expUi, islands);
        Assert.Equal(Color.Parse("#FFCFE2FF"), islands);
    }

    /// <summary>只装控件层的宿主不会凭空多出外壳键 —— 派生只覆盖这套主题**本来就有**的键。</summary>
    [AvaloniaFact]
    public void 只装控件层时不产出外壳键()
    {
        var theme = new ForgeControlsTheme { Family = ForgeThemeFamily.ExpUi, AccentLight = Blue, AccentDark = Orange };

        Assert.Equal(Blue, BrushColor(theme, "Forge.Button.Accent.Background", ThemeVariant.Light));
        Assert.False(theme.TryGetResource("Forge.ExpUi.ButtonSelectedBackground", ThemeVariant.Light, out _));
    }

    /// <summary>
    /// 已经装着同族主题时再 <c>Apply</c> 一个带种子色的，种子色要**接过去**（而不是被整个跳过）。
    /// </summary>
    /// <remarks>
    /// <b>反证记录。</b><see cref="ForgeThemeManager.Apply"/> 原来的判据是"同族就 return" ——
    /// 那时一族只对应一种长相，两个同族实例确实等价；种子色加进来之后就不等价了，
    /// 宿主设的主题色会一声不响地没生效（本条正是这么被抓出来的）。
    /// <para>
    /// 修法是**接过去而不是换掉 Styles**：换 Styles 会让已挂树的控件重新套模板（该类开头那条
    /// "换主题要重启"就是这个原因，实测里它会当场抛"控件已经有父级"）；而种子色只是资源值、
    /// 由 <c>{DynamicResource}</c> 取，接过去不需要重新套模板。
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void 同族重复Apply会把种子色接过去()
    {
        using var scope = ForgeThemeScope.Use(ForgeThemeScope.Default);
        var app = Application.Current!;
        var before = app.Styles.Count;

        ForgeThemeManager.Apply(app, new ForgeTheme
        {
            Family = ForgeThemeScope.Default,
            AccentLight = Blue,
            AccentDark = Orange,
        });

        Assert.Equal(before, app.Styles.Count);   // 没换 Styles
        Assert.True(app.TryFindResource("Forge.Button.Accent.Background", ThemeVariant.Light, out var value));
        Assert.Equal(Blue, Assert.IsAssignableFrom<ISolidColorBrush>(value).Color);

        // 收尾：把种子色摘掉，免得染到后面的用例（ForgeThemeScope 只还原族，不还原种子色）。
        ForgeThemeManager.Apply(app, new ForgeTheme { Family = ForgeThemeScope.Default });
    }

    private static ForgeTheme Seeded(ForgeThemeFamily family) =>
        new() { Family = family, AccentLight = Blue, AccentDark = Orange };

    /// <summary>控件键的值是 <c>SolidColorBrush</c>（控件模板绑的是 IBrush）。</summary>
    private static Color BrushColor(ForgeThemeBase theme, string key, ThemeVariant variant)
    {
        Assert.True(theme.TryGetResource(key, variant, out var value), $"找不到 {key}");
        return Assert.IsAssignableFrom<ISolidColorBrush>(value).Color;
    }

    /// <summary>外壳键的值是 <c>Color</c>（外壳模板每族一份，绑的是 Color）。类型写反不报错，只是覆盖静默失效。</summary>
    private static Color ShellColor(ForgeThemeBase theme, ForgeThemeFamily family, string suffix, ThemeVariant variant)
    {
        var key = $"Forge.{family}.{suffix}";
        Assert.True(theme.TryGetResource(key, variant, out var value), $"找不到 {key}");
        return Assert.IsType<Color>(value);
    }
}
