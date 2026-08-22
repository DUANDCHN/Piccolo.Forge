namespace Piccolo.Forge.Themes;

/// <summary>
/// 缩放。所有未缩放值都从这里过。
/// </summary>
/// <remarks>
/// 对应上游 <c>platform/util/ui/src/com/intellij/ui/scale/JBUIScale.kt</c>。
/// 倍率归根到底是从**字号**推出来的，不是直接从 DPI：
/// IDE 缩放先改字号，字号再反推倍率（<c>LafManagerImpl.kt:852-880 · patchLafFonts</c>），
/// 而且两条路最后都会被离散化到 0.25 的整数倍（见 <see cref="Discretize"/>）。
/// </remarks>
public static class ForgeScale
{
    /// <summary>离散化的粒度 0.25（<c>JBUIScale.kt:37 · DISCRETE_SCALE_RESOLUTION</c>）。</summary>
    public const float DiscreteScaleResolution = 0.25f;

    /// <summary>系统基准字号 12（<c>JBUIScale.kt:113 · DEF_SYSTEM_FONT_SIZE</c>）。</summary>
    public const float DefaultSystemFontSize = 12f;

    /// <summary>
    /// 整数缩放，**四舍五入**。
    /// </summary>
    /// <remarks>
    /// 对应 <c>JBUIScale.kt:399-401</c>：<c>(userScaleFactor * i).roundToInt()</c>。
    /// Kotlin 的 <c>roundToInt</c> 是 <c>.5</c> 向上取（等价于 <see cref="MidpointRounding.AwayFromZero"/>
    /// 在正数上的表现），**不是**截断，也不是 .NET 默认的「银行家舍入」。
    /// 逐像素对齐必须照抄这个取整。
    /// </remarks>
    /// <param name="value">未缩放的整数。</param>
    /// <param name="userScaleFactor">用户缩放倍率。</param>
    /// <returns>缩放后的整数。</returns>
    public static int Scale(int value, float userScaleFactor)
        => (int)Math.Floor((userScaleFactor * value) + 0.5f);

    /// <summary>
    /// 浮点缩放，直接相乘。
    /// </summary>
    /// <remarks>对应 <c>JBUIScale.kt:391-393</c>。</remarks>
    /// <param name="value">未缩放的值。</param>
    /// <param name="userScaleFactor">用户缩放倍率。</param>
    /// <returns>缩放后的值。</returns>
    public static float Scale(float value, float userScaleFactor) => value * userScaleFactor;

    /// <summary>
    /// 字号缩放：两个硬编码特例，且结果**截断**不是四舍五入。
    /// </summary>
    /// <remarks>
    /// 对应 <c>JBUIScale.kt:421-426 · scaleFontSize</c>：
    /// 倍率 1.25 时乘 1.34，1.75 时乘 1.67，其余乘倍率本身，最后 <c>toInt()</c> 截断。
    /// </remarks>
    /// <param name="fontSize">未缩放字号。</param>
    /// <param name="userScaleFactor">用户缩放倍率。</param>
    /// <returns>缩放后的字号。</returns>
    public static int ScaleFontSize(float fontSize, float userScaleFactor)
    {
        var scaled = userScaleFactor switch
        {
            1.25f => fontSize * 1.34f,
            1.75f => fontSize * 1.67f,
            _ => fontSize * userScaleFactor,
        };

        return (int)scaled;
    }

    /// <summary>
    /// 把倍率对齐到 <see cref="DiscreteScaleResolution"/> 的整数倍。
    /// </summary>
    /// <remarks>
    /// 对应 <c>JBUIScale.kt:490-495 · discreteScale</c>（默认四舍五入；
    /// Registry <c>ide.scale.discrete.take.floor</c> 打开时改成取下整，这里不做那个分支）。
    /// 无论是 HiDPI 推出来的倍率还是 IDE 缩放推出来的，
    /// 最后都过这一道（<c>:331</c>、<c>:298</c>）。
    /// </remarks>
    /// <param name="scale">原始倍率。</param>
    /// <returns>对齐后的倍率。</returns>
    public static float Discretize(float scale)
        => (float)Math.Floor((scale / DiscreteScaleResolution) + 0.5f) * DiscreteScaleResolution;

    /// <summary>
    /// 字号 → 倍率。
    /// </summary>
    /// <remarks>对应 <c>JBUIScale.kt:270 · getFontScale</c>：<c>fontSize / 12</c>。</remarks>
    /// <param name="fontSize">字号。</param>
    /// <returns>倍率（未离散化）。</returns>
    public static float GetFontScale(float fontSize) => fontSize / DefaultSystemFontSize;
}
