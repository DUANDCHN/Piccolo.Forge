namespace Piccolo.Forge.Themes;

/// <summary>
/// 岛形态的度量，**未缩放值**（unscaled）。逐条出处见 <c>docs/upstream/Islands主题.md</c> §2。
/// </summary>
/// <remarks>
/// 这些数字**按主题变**，不像 <see cref="ForgeMetrics"/> 那样全局唯一：
/// expUI（方角、面板贴死）根本没有岛的概念，对应
/// <see cref="ForgePalette.Island"/> 为 <see langword="null"/>。
/// 判「是不是岛形态」一律看那个属性是否为 <see langword="null"/>，别拿 <see cref="Arc"/> 等于 0 去猜。
/// <para>
/// 三份 Islands 主题 JSON 里的 <c>Island</c> 块逐字节相同
/// （<c>ManyIslandsDark.theme.json:412-423</c>），所以这里只有「常规 / 紧凑」两档，
/// 不按明暗分。
/// </para>
/// </remarks>
public sealed record ForgeIslandMetrics
{
    /// <summary>
    /// 圆角**直径**（<c>Island.arc</c>）。圆角半径是它的一半。
    /// </summary>
    /// <remarks>
    /// 上游两处都把它当直径用：抠角的控制距离是 <c>scale(arc / 2f)</c>
    /// （<c>IslandsUICustomization.kt:965</c>），描边的圆弧是
    /// <c>drawArc(offset, offset, arcSize, arcSize, 90, 90)</c>（<c>:1022</c>），
    /// AWT 的 <c>drawArc</c> 前两个尺寸参数是外接矩形的宽高。
    /// 当成半径用会得到两倍大的圆角。
    /// </remarks>
    public required double Arc { get; init; }

    /// <summary>
    /// 缝的**总宽**（<c>Island.borderWidth</c>）。单边让出的是它的一半。
    /// </summary>
    /// <remarks><c>IslandsUICustomization.kt:957 · scale(getInt("Island.borderWidth", 6) / 2)</c>。</remarks>
    public required double BorderWidth { get; init; }

    /// <summary>
    /// 描边直线段两端各让出的长度（<c>Island.borderArcLength</c>），把位置留给圆弧。
    /// </summary>
    /// <remarks><c>IslandsUICustomization.kt:1013</c>。</remarks>
    public required double BorderArcLength { get; init; }

    /// <summary>工具窗口的内边距（<c>Island.ToolWindow.insets</c>，<c>IslandsUICustomization.kt:868</c>）。</summary>
    public required double ToolWindowInset { get; init; }

    /// <summary>编辑区的内边距（<c>Island.Editor.insets</c>，<c>IslandsUICustomization.kt:880</c>）。</summary>
    public required double EditorInset { get; init; }

    /// <summary>单边让出的缝宽 = <see cref="BorderWidth"/> 的一半。</summary>
    public double HalfBorderWidth => BorderWidth / 2;

    /// <summary>圆角半径 = <see cref="Arc"/> 的一半。</summary>
    public double CornerRadius => Arc / 2;

    /// <summary>常规档。</summary>
    public static ForgeIslandMetrics Default { get; } = new()
    {
        Arc = 20,
        BorderWidth = 6,
        BorderArcLength = 14,
        ToolWindowInset = 3,
        EditorInset = 2,
    };

    /// <summary>
    /// 紧凑档（<c>Island.arc.compact</c> 等）。
    /// </summary>
    /// <remarks>
    /// 内边距**没有** compact 变体：上游 <c>Island.ToolWindow.insets</c> /
    /// <c>Island.Editor.insets</c> 两个键在元数据里都只有一档
    /// （<c>IntelliJPlatform.themeMetadata.json:1420-1462</c> 里 compact 变体只列了
    /// arc / borderArcLength / borderWidth 三个），故沿用常规值。
    /// </remarks>
    public static ForgeIslandMetrics Compact { get; } = Default with
    {
        Arc = 16,
        BorderWidth = 4,
        BorderArcLength = 10,
    };
}
