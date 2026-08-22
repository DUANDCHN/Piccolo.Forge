namespace Piccolo.Forge.Themes;

/// <summary>
/// 四边内边距，字段顺序与上游主题 JSON 的字面量一致：<c>"top,left,bottom,right"</c>。
/// </summary>
/// <param name="Top">上。</param>
/// <param name="Left">左。</param>
/// <param name="Bottom">下。</param>
/// <param name="Right">右。</param>
/// <remarks>
/// **顺序是 TLBR，不是 CSS 的 TRBL，也不是 Avalonia <c>Thickness</c> 的 LTRB。**
/// 抄主题值时按位置对号入座，别按习惯猜——Islands 的左右条带内边距刻意不对称
/// （`5,5,5,2` / `5,2,5,5`），顺序错了会把图标推向相反的一侧，而且左右各错一次、
/// 看起来"两边都偏了一点"，很难反推回是顺序问题。
/// </remarks>
public readonly record struct ForgeInsets(double Top, double Left, double Bottom, double Right)
{
    /// <summary>四边等宽。</summary>
    /// <param name="all">四边共同的值。</param>
    public ForgeInsets(double all) : this(all, all, all, all)
    {
    }

    /// <summary>左右之和。</summary>
    public double Horizontal => Left + Right;

    /// <summary>上下之和。</summary>
    public double Vertical => Top + Bottom;
}

/// <summary>
/// 条带按钮的度量，**未缩放值**（unscaled）。
/// </summary>
/// <remarks>
/// 这一组**按主题变**：expUI 是 40×40、四边内边距相等；Islands 收窄到 37×40，
/// 且左右条带的内边距**不对称**——靠窗口内侧留 2、外侧留 5，等于把图标推向窗口边缘。
/// 逐条出处见 <c>docs/upstream/Islands主题.md</c> §4。
/// <para>
/// 上游的键是拼出来的（<c>JBUI.java:1343</c>）：
/// <c>"StripeToolbar.Button." + (left ? "left" : "right") + "StripeIcon" + (showNames ? "WithName" : "") + ".padding"</c>，
/// 所以是 **左/右 × 显名/不显名 = 四份**，本类照这个形状存。
/// </para>
/// <para>
/// <b>紧凑档不是"另一套值"，是覆盖</b>：上游切紧凑时把所有 <c>X.compact</c> 的值盖到 <c>X</c> 上
/// （<c>LafManagerImpl.kt:1466-1474 · applyDensityOnUpdateUi</c>），
/// **没有 <c>.compact</c> 变体的键保留基值**，而不是回落到代码兜底。
/// </para>
/// </remarks>
public sealed record ForgeStripeButtonMetrics
{
    /// <summary>按钮宽（<c>StripeToolbar.Button.size</c> 的第一个分量）。</summary>
    public required double Width { get; init; }

    /// <summary>按钮高（同键第二个分量）。</summary>
    public required double Height { get; init; }

    /// <summary>图标边长（<c>StripeToolbar.Button.iconSize</c>）。</summary>
    public required double IconSize { get; init; }

    /// <summary>左条带、不显名（<c>leftStripeIcon.padding</c>）。</summary>
    public required ForgeInsets LeftIconPadding { get; init; }

    /// <summary>右条带、不显名（<c>rightStripeIcon.padding</c>）。</summary>
    public required ForgeInsets RightIconPadding { get; init; }

    /// <summary>左条带、显名（<c>leftStripeIconWithName.padding</c>）。</summary>
    public required ForgeInsets LeftIconWithNamePadding { get; init; }

    /// <summary>右条带、显名（<c>rightStripeIconWithName.padding</c>）。</summary>
    public required ForgeInsets RightIconWithNamePadding { get; init; }

    /// <summary>左条带名字的横向偏移（<c>leftStripeTextOffset</c>）。</summary>
    public required double LeftTextOffset { get; init; }

    /// <summary>右条带名字的横向偏移（<c>rightStripeTextOffset</c>）。</summary>
    public required double RightTextOffset { get; init; }

    /// <summary>按左右 × 显名与否挑一份内边距。</summary>
    /// <param name="isLeft">是不是左条带。</param>
    /// <param name="showName">是不是「显示名字」模式。</param>
    /// <returns>该用的内边距。</returns>
    public ForgeInsets IconPadding(bool isLeft, bool showName) => (isLeft, showName) switch
    {
        (true, false) => LeftIconPadding,
        (false, false) => RightIconPadding,
        (true, true) => LeftIconWithNamePadding,
        (false, true) => RightIconWithNamePadding,
    };

    /// <summary>按左右挑名字的横向偏移。</summary>
    /// <param name="isLeft">是不是左条带。</param>
    /// <returns>偏移量。</returns>
    public double TextOffset(bool isLeft) => isLeft ? LeftTextOffset : RightTextOffset;

    /// <summary>按配色族与密度挑一套。</summary>
    /// <param name="family">配色族。</param>
    /// <param name="compact">是否紧凑模式。</param>
    /// <returns>该用的度量。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="family"/> 不是已知的族。</exception>
    public static ForgeStripeButtonMetrics ForTheme(ForgeThemeFamily family, bool compact) => family switch
    {
        ForgeThemeFamily.ExpUi => compact ? ExpUiCompact : ExpUi,
        ForgeThemeFamily.Islands => compact ? IslandsCompact : Islands,
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, null),
    };

    /// <summary>
    /// expUI 常规档。
    /// </summary>
    /// <remarks>
    /// expUI 的两份主题 JSON **没有** <c>StripeToolbar.Button</c> 块，值全部来自共同的根
    /// <c>darcula.theme.json:836-850</c> 与代码兜底（尺寸 40×40 见 <c>JBUI.java:1322-1324</c>、
    /// 图标 20 见 <c>:1334-1336</c>、内边距兜底 <c>insets(5)</c> 见 <c>:1346-1348</c>）。
    /// </remarks>
    public static ForgeStripeButtonMetrics ExpUi { get; } = new()
    {
        Width = 40,
        Height = 40,
        IconSize = 20,
        LeftIconPadding = new ForgeInsets(5),                    // darcula:839
        RightIconPadding = new ForgeInsets(5),                   // darcula:840
        LeftIconWithNamePadding = new ForgeInsets(4),            // darcula:843
        RightIconWithNamePadding = new ForgeInsets(4),           // darcula:844
        LeftTextOffset = 0,                                      // darcula:846
        RightTextOffset = 0,                                     // darcula:848
    };

    /// <summary>
    /// expUI 紧凑档。
    /// </summary>
    /// <remarks>
    /// ⚠ <b><see cref="RightIconWithNamePadding"/> 仍是 4，不是 5。</b>
    /// <c>rightStripeIconWithName.padding.compact</c> 在 <c>darcula.theme.json</c> 里**不存在**
    /// （左边那个 `:845` 有，右边没有），而紧凑是「有 <c>.compact</c> 才覆盖」，
    /// 于是它保留基值 `:844` 的 4。这里曾被读成"回落到代码兜底 5"——
    /// 回落只在键整个缺席时才发生，而它并不缺席。
    /// </remarks>
    public static ForgeStripeButtonMetrics ExpUiCompact { get; } = ExpUi with
    {
        Width = 32,                                              // darcula:837 size.compact
        Height = 32,
        IconSize = 16,                                           // darcula:838
        LeftIconPadding = new ForgeInsets(4),                    // darcula:841
        RightIconPadding = new ForgeInsets(4),                   // darcula:842
        LeftIconWithNamePadding = new ForgeInsets(3),            // darcula:845
        // RightIconWithNamePadding 不写 —— 沿用 ExpUi 的 4，理由见上面的 ⚠
    };

    /// <summary>
    /// Islands 常规档（<c>ManyIslandsDark.theme.json:1191-1205</c>，三份 Islands 主题相同）。
    /// </summary>
    /// <remarks>
    /// 按钮 **37 宽 × 40 高**，且左右内边距镜像对称：靠窗口内侧那边留 2、外侧留 5。
    /// 两套算下来高亮矩形都是 30×30（<c>40-5-5</c> 与 <c>37-5-2</c>），
    /// Islands 只是把它整体推向条带外缘。
    /// </remarks>
    public static ForgeStripeButtonMetrics Islands { get; } = new()
    {
        Width = 37,                                              // islands:1192
        Height = 40,
        IconSize = 20,                                           // 未覆盖，沿用兜底
        LeftIconPadding = new ForgeInsets(5, 5, 5, 2),           // islands:1193
        RightIconPadding = new ForgeInsets(5, 2, 5, 5),          // islands:1194
        LeftIconWithNamePadding = new ForgeInsets(4, 6, 4, 2),   // islands:1197
        RightIconWithNamePadding = new ForgeInsets(4, 2, 4, 6),  // islands:1198
        LeftTextOffset = 2,                                      // islands:1201
        RightTextOffset = -2,                                    // islands:1203
    };

    /// <summary>
    /// Islands 紧凑档。
    /// </summary>
    /// <remarks>
    /// Islands 主题**没有**覆盖 <c>size.compact</c> / <c>iconSize.compact</c>，
    /// 那两个仍来自共同的根 <c>darcula.theme.json:837-838</c>——
    /// 即紧凑下 Islands 的按钮退回 **32×32 正方**，那条"37 宽"只在常规档成立。
    /// </remarks>
    public static ForgeStripeButtonMetrics IslandsCompact { get; } = Islands with
    {
        Width = 32,                                              // darcula:837（Islands 未覆盖）
        Height = 32,
        IconSize = 16,                                           // darcula:838
        LeftIconPadding = new ForgeInsets(4, 5, 4, 3),           // islands:1195
        RightIconPadding = new ForgeInsets(4, 3, 4, 5),          // islands:1196
        LeftIconWithNamePadding = new ForgeInsets(3, 4, 3, 2),   // islands:1199
        RightIconWithNamePadding = new ForgeInsets(3, 2, 3, 4),  // islands:1200
        LeftTextOffset = 0,                                      // islands:1202
        RightTextOffset = 0,                                     // islands:1204
    };
}
