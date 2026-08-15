namespace Piccolo.Forge.Model;

/// <summary>
/// 浮动窗口失活后淡出、激活后淡入。
/// </summary>
/// <remarks>
/// 对应 <c>platform/platform-impl/src/com/intellij/openapi/wm/impl/FloatingDecorator.java:285-318</c>。
/// **默认关闭**（`UISettings.enableAlphaMode` 默认 false，<c>UISettingsState.kt:180</c>）。
/// <para>
/// 这里只做纯粹的插值算术，定时器交给视图层 —— 这样能不建 UI 就测。
/// </para>
/// </remarks>
/// <param name="StartRatio">起始透明度比例（0 = 完全不透明）。</param>
/// <param name="EndRatio">目标透明度比例。</param>
/// <param name="TotalFrames">总帧数。</param>
public readonly record struct FloatingFade(float StartRatio, float EndRatio, int TotalFrames)
{
    /// <summary>
    /// 第 <paramref name="frame"/> 帧的透明度比例。
    /// </summary>
    /// <remarks>
    /// 对应 <c>:311-318 · getCurrentAlphaRatio</c>：线性插值，
    /// 但**变不透明的方向速度加倍**（<c>delta *= 2</c>，注释写的是
    /// 「dialog is becoming non transparent quicker」），最后夹到 [0,1]。
    /// </remarks>
    /// <param name="frame">当前帧号。</param>
    /// <returns>透明度比例。</returns>
    public float RatioAt(int frame)
    {
        if (TotalFrames <= 0)
        {
            return EndRatio;
        }

        var delta = (EndRatio - StartRatio) / TotalFrames;
        if (StartRatio > EndRatio)
        {
            delta *= 2;
        }

        return Math.Clamp(StartRatio + (frame * delta), 0f, 1f);
    }

    /// <summary>
    /// 窗口激活时该播的动画：立刻往完全不透明去。
    /// </summary>
    /// <remarks>对应 <c>:289-296</c>，激活分支没有延迟。</remarks>
    /// <param name="currentRatio">当前透明度比例。</param>
    /// <param name="totalFrames">总帧数。</param>
    /// <returns>动画参数。</returns>
    public static FloatingFade OnActivated(float currentRatio, int totalFrames)
        => new(currentRatio, 0f, totalFrames);

    /// <summary>
    /// 窗口失活时该播的动画：等一会儿再往目标透明度去。
    /// </summary>
    /// <remarks>对应 <c>:298-310</c>，延迟是 `alphaModeDelay`（1500ms）。</remarks>
    /// <param name="currentRatio">当前透明度比例。</param>
    /// <param name="targetRatio">目标透明度比例。</param>
    /// <param name="totalFrames">总帧数。</param>
    /// <returns>动画参数。</returns>
    public static FloatingFade OnDeactivated(float currentRatio, float targetRatio, int totalFrames)
        => new(currentRatio, targetRatio, totalFrames);

    /// <summary>
    /// 动画被打断重来时，当前帧号要翻过来。
    /// </summary>
    /// <remarks>
    /// 对应 <c>:291-293</c> 与 <c>:302-304</c>：
    /// <c>if (currentFrame > 0) currentFrame = TOTAL_FRAME_COUNT - currentFrame</c>。
    /// 不这么做，来回快速切换焦点时动画会跳。
    /// </remarks>
    /// <param name="currentFrame">被打断时的帧号。</param>
    /// <param name="totalFrames">总帧数。</param>
    /// <returns>翻过来的帧号。</returns>
    public static int FlipFrame(int currentFrame, int totalFrames)
        => currentFrame > 0 ? totalFrames - currentFrame : currentFrame;
}
