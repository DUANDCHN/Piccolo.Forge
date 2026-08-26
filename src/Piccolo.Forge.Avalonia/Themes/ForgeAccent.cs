using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Styling;

namespace Piccolo.Forge.Themes;

/// <summary>
/// 主题色（强调色）的派生：宿主给一支**种子色**，这里把色板里那一批跟着强调色走的键全算出来。
/// </summary>
/// <remarks>
/// <para>
/// <b>由来。</b>「换掉主题色」在此之前是宿主自己逐键覆盖 —— 控件那半 9 个键（不带族、Brush）
/// 加外壳那半每族 8 个键（带族、Color），两族明暗四档下来是 <b>37 处手工色值</b>。
/// 那不是配置，那是把库的调色板抄了一遍：漏一处就是"面板里变了、侧边栏还是原色"，
/// 而且漏了不报错。种子键把它收成明暗各一支。
/// </para>
/// <para>
/// <b>派生规则只有两条</b>，按键的**档位**（<see cref="Tier"/>，显式写在名单里）分：
/// <list type="number">
/// <item><b>实心填充档</b>（按钮主色、焦点环、勾选框、标签下划线、进度条、条带按钮选中底、
/// 面板标题条下划线）→ 直接取<b>种子色原值</b>。</item>
/// <item><b>派生档</b>（浅色底、软底、拖放蒙版）→ <b>保留原值的明度 L</b>、
/// 饱和度按「种子 S ÷ 本族强调色 S」等比缩放、<b>色相换成种子的色相</b>。
/// 明度阶梯是主题作者调好的（哪支该浅、哪支该深、文字压上去够不够对比度），种子不该动它；
/// 而 S 等比缩放是为了让**低饱和的种子**（想要一套灰调界面）能把那批浅底一起拉灰，
/// 不是只把色相转了、饱和度还钉在原处。</item>
/// </list>
/// 派生档的 L / S **从已经合入的族色板里现读**，不在这里存第二份色值 —— 色板改了自动跟着走。
/// </para>
/// <para>
/// <b>alpha 原样保留。</b>拖放蒙版那几个键带 alpha（如 <c>#4D366ACF</c>），那个透明度是叠加强度、
/// 不是颜色，只换 RGB 段。
/// </para>
/// <para>
/// ⚠ <b>中性色不动</b>（<see cref="NeutralSaturation"/>）：名单里有几个键在某一族/某一档下本来就是
/// 中性的 —— 经典档的编辑区选中标签底是 <c>#FFFFFF</c> / <c>#1E1F22</c>、描边是全透明。
/// 给它们转色相只会得到一块"偏暖的灰"，而它们根本不是主题色。判据取 S &lt; 0.15：
/// 名单里真正跟着强调色走的键最低 S 是 0.43（Islands 暗档的选中标签底），中性的那几个最高 0.06，
/// 中间是一条很宽的空带，阈值取在哪儿都一样。
/// </para>
/// </remarks>
internal static class ForgeAccent
{
    /// <summary>低于这个饱和度就当中性色，不参与派生（理由见类注释）。</summary>
    private const double NeutralSaturation = 0.15;

    /// <summary>
    /// 强调色键的两档。<b>分档是设计知识，只能显式写</b> —— 曾经想按"原值是不是等于本族强调色"
    /// 自动判，被 Islands 的色板反证掉：它的 <c>Forge.Check.Selected*</c> 是 <c>#3574F0</c>
    /// （生成器注释写明"上游 JSON 未定义，取自 expUI 的 checkBoxSelected.svg"），
    /// 而它的强调色是 <c>#3871E1</c> —— 两者差一点点，自动判就把勾选框错判成派生档，
    /// 算出一支过饱和的怪色。上游色板本身就有这种不齐，判据不能建在"它们恰好相等"上。
    /// </summary>
    private enum Tier
    {
        /// <summary>实心填充：取种子原值。</summary>
        Seed,

        /// <summary>派生：保 L、S 等比缩放、换色相。</summary>
        Derived,
    }

    /// <summary>控件那半的强调色键。**不带族前缀**（控件模板两族共用一份），值是 <c>SolidColorBrush</c>。</summary>
    private static readonly (string Key, Tier Tier)[] ControlKeys =
    [
        ("Forge.Button.Accent.Background", Tier.Seed),
        ("Forge.Focus.Ring", Tier.Seed),
        // 输入框悬停时 1px 的边框本身变主题色（聚焦另有一圈 2px 外环，两者形状不同、分得开）。
        ("Forge.Input.HoverBorder", Tier.Seed),
        // 聚焦时边框自己变主题色（本库不画上游那圈光晕，颜色只能由边框扛）。
        ("Forge.Input.FocusedBorder", Tier.Seed),
        // 菜单选中是**实心**一行（不是列表那种淡底），跟着种子走。
        ("Forge.MenuItem.SelectedBackground", Tier.Seed),
        ("Forge.Check.SelectedBackground", Tier.Seed),
        ("Forge.Check.SelectedBorder", Tier.Seed),
        // ⚠ Islands 的这支是**全透明**（那一族不画标签下划线）。派生保留原值的 alpha，
        //   所以它照样是透明的 —— 不会因为"归到实心填充档"就凭空多画一条线出来。
        ("Forge.Tab.Underline", Tier.Seed),
        // ⚠ 经典档暗色的原值是 #467FF2，比强调色亮一档（上游给进度条多一点存在感）。
        //   归到实心档等于放弃这一档细微差别 —— 取舍是"主题色只有一支"比这处 nuance 重要。
        ("Forge.Progress.Bar", Tier.Seed),

        ("Forge.Item.SelectedBackground", Tier.Derived),
        ("Forge.Input.SelectionBackground", Tier.Derived),
    ];

    /// <summary>
    /// 外壳那半的强调色键**后缀**。实际键名带族前缀（<c>Forge.ExpUi.*</c> / <c>Forge.Islands.*</c>，
    /// 外壳模板每族一份），值是 <c>Color</c>。
    /// </summary>
    private static readonly (string Suffix, Tier Tier)[] ShellSuffixes =
    [
        ("ButtonSelectedBackground", Tier.Seed),
        ("HeaderTabUnderline", Tier.Seed),

        // 拖放那几个是**故意比强调色浅/带透明**的指示层，归派生档，各自的明度与 alpha 都留着。
        ("StripeSeparatorDragAndDrop", Tier.Derived),
        ("ButtonDropBackground", Tier.Derived),
        ("ButtonDropBorder", Tier.Derived),
        ("DragAreaBackground", Tier.Derived),
        // 经典档这两支本来就是中性（亮 #FFFFFF / 暗 #1E1F22、描边全透明），
        // 会被中性判据挡下，只有 Islands 那份真正跟着色相走。
        ("EditorTabSelectedBackground", Tier.Derived),
        ("EditorTabSelectedBorder", Tier.Derived),
    ];

    /// <summary>控件那半算 S 比例时的基准键（= 该族该档的强调色本身）。</summary>
    private const string ControlReference = "Forge.Button.Accent.Background";

    /// <summary>外壳那半的基准键后缀（条带按钮选中底在两族都恰好就是强调色原值）。</summary>
    private const string ShellReference = "ButtonSelectedBackground";

    /// <summary>
    /// 按种子色算出一档（明或暗）的全部派生键。
    /// </summary>
    /// <param name="lookup">
    /// 读**本族色板原值**用的回调（键 → 值）。取不到就跳过那个键 ——
    /// 只装了控件层或只装了外壳层的宿主（<c>ForgeControlsTheme</c> / <c>ForgeShellTheme</c>）
    /// 本来就只有一半键在。
    /// </param>
    /// <param name="family">族，用来拼外壳键的前缀。</param>
    /// <param name="seed">种子色。</param>
    /// <param name="controls">要不要算控件那半。</param>
    /// <param name="shell">要不要算外壳那半。</param>
    /// <returns>键 → 值。控件键给 <c>SolidColorBrush</c>，外壳键给 <c>Color</c> —— 类型必须与色板一致。</returns>
    public static Dictionary<object, object> Derive(
        Func<string, Color?> lookup, ForgeThemeFamily family, Color seed, bool controls, bool shell)
    {
        var result = new Dictionary<object, object>();
        var seedHsl = ForgeHsl.From(seed);

        if (controls && lookup(ControlReference) is { } controlAccent)
            foreach (var (key, tier) in ControlKeys)
                if (lookup(key) is { } stock)
                    result[key] = new SolidColorBrush(Derive(stock, tier, controlAccent, seed, seedHsl));

        if (shell)
        {
            var prefix = $"Forge.{family}.";
            if (lookup(prefix + ShellReference) is { } shellAccent)
                foreach (var (suffix, tier) in ShellSuffixes)
                    if (lookup(prefix + suffix) is { } stock)
                        result[prefix + suffix] = Derive(stock, tier, shellAccent, seed, seedHsl);
        }

        return result;
    }

    /// <param name="stock">该键在本族色板里的原值。</param>
    /// <param name="tier">该键属于哪一档。</param>
    /// <param name="familyAccent">本族本档的强调色原值，算 S 比例的分母。</param>
    /// <param name="seed">种子色。</param>
    /// <param name="seedHsl">种子色的 HSL，外面算好传进来，免得每个键重算一遍。</param>
    private static Color Derive(Color stock, Tier tier, Color familyAccent, Color seed, ForgeHsl seedHsl)
    {
        // 规则①：实心填充 → 种子原值。**alpha 跟着原值走**，Islands 那支全透明的下划线因此仍是透明的。
        if (tier == Tier.Seed) return Color.FromArgb(stock.A, seed.R, seed.G, seed.B);

        var hsl = ForgeHsl.From(stock);

        // 中性色不参与派生（理由见类注释）。
        if (hsl.S < NeutralSaturation) return stock;

        // 规则②：保 L、S 等比缩放、色相换成种子的。
        var accentS = ForgeHsl.From(familyAccent).S;
        var scale = accentS <= 0.001 ? 1.0 : seedHsl.S / accentS;   // 族强调色是灰时不缩放，免得除爆
        var derived = new ForgeHsl(seedHsl.H, Math.Clamp(hsl.S * scale, 0, 1), hsl.L).ToColor();
        return Color.FromArgb(stock.A, derived.R, derived.G, derived.B);
    }
}

/// <summary>HSL 三元组。<see cref="ForgeAccent"/> 的派生规则按明度/饱和度表述，故要在两种表示间来回。</summary>
/// <remarks>
/// 自己写而不用现成库：Forge 的 nuspec 只依赖 Avalonia（对标 Semi），为二十行数学多背一个包不值当。
/// 公式取自 CSS Color Level 3 的 HSL ↔ sRGB，与浏览器 / 设计工具算出来的一致。
/// </remarks>
internal readonly record struct ForgeHsl(double H, double S, double L)
{
    /// <summary>从 sRGB 取 HSL（H 单位是度，S / L 是 0..1）。</summary>
    public static ForgeHsl From(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2.0, d = max - min;

        if (d <= 0.0) return new ForgeHsl(0, 0, l);

        double s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
        double h = max == r ? (g - b) / d + (g < b ? 6.0 : 0.0)
                 : max == g ? (b - r) / d + 2.0
                 : (r - g) / d + 4.0;
        return new ForgeHsl(h * 60.0, s, l);
    }

    /// <summary>回到 sRGB（alpha 由调用方决定，这里只给颜色）。</summary>
    public Color ToColor()
    {
        if (S <= 0.0)
        {
            var v = Round(L);
            return Color.FromRgb(v, v, v);
        }

        double q = L < 0.5 ? L * (1.0 + S) : L + S - L * S;
        double p = 2.0 * L - q;
        double h = H / 360.0;
        return Color.FromRgb(Round(Hue(p, q, h + 1.0 / 3.0)), Round(Hue(p, q, h)), Round(Hue(p, q, h - 1.0 / 3.0)));
    }

    private static double Hue(double p, double q, double t)
    {
        if (t < 0.0) t += 1.0;
        if (t > 1.0) t -= 1.0;
        if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
        return p;
    }

    private static byte Round(double v) => (byte)Math.Clamp(Math.Round(v * 255.0, MidpointRounding.AwayFromZero), 0, 255);
}
