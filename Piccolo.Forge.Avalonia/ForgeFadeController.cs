using Avalonia.Controls;
using Avalonia.Threading;
using Piccolo.Forge.Model;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 驱动浮动窗口的淡入淡出。
/// </summary>
/// <remarks>
/// 插值在模型层（<see cref="FloatingFade"/>），这里只管定时器与窗口的 Opacity。
/// 规则见 <c>工具窗口模型.md</c> §14.4：
/// 激活立刻淡回不透明，失活先等 <see cref="ForgeMetrics.FloatingFadeDelayMs"/> 再淡出。
/// **默认关闭**（<see cref="ForgeMetrics.FloatingFadeEnabledByDefault"/>）。
/// </remarks>
public sealed class ForgeFadeController : IDisposable
{
    private readonly Window _window;
    private readonly DispatcherTimer _ticker;
    private readonly DispatcherTimer _delay;

    private FloatingFade _fade;
    private int _frame;

    /// <summary>
    /// 接上一个窗口。
    /// </summary>
    /// <param name="window">要淡入淡出的窗口。</param>
    /// <param name="targetRatio">失活后的目标透明度比例。</param>
    public ForgeFadeController(Window window, float targetRatio = ForgeMetrics.FloatingFadeRatio)
    {
        ArgumentNullException.ThrowIfNull(window);

        _window = window;
        TargetRatio = targetRatio;

        _ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ForgeMetrics.FloatingFadeFrameDelayMs) };
        _ticker.Tick += (_, _) => Tick();

        _delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ForgeMetrics.FloatingFadeDelayMs) };
        _delay.Tick += (_, _) =>
        {
            _delay.Stop();
            Start(FloatingFade.OnDeactivated(CurrentRatio, TargetRatio, ForgeMetrics.FloatingFadeTotalFrames));
        };
    }

    /// <summary>失活后的目标透明度比例。</summary>
    public float TargetRatio { get; }

    /// <summary>当前透明度比例（0 = 完全不透明）。</summary>
    public float CurrentRatio { get; private set; }

    /// <summary>动画在不在跑。</summary>
    public bool IsRunning => _ticker.IsEnabled;

    /// <summary>
    /// 窗口拿到焦点：立刻往不透明去，没有延迟。
    /// </summary>
    /// <remarks>对应 <c>FloatingDecorator.java:289-296</c>。</remarks>
    public void OnActivated()
    {
        _delay.Stop();
        Start(FloatingFade.OnActivated(CurrentRatio, ForgeMetrics.FloatingFadeTotalFrames));
    }

    /// <summary>
    /// 窗口失去焦点：等一会儿再淡出。
    /// </summary>
    /// <remarks>对应 <c>FloatingDecorator.java:298-310</c>，延迟 1500ms。</remarks>
    public void OnDeactivated()
    {
        _ticker.Stop();
        _delay.Stop();
        _delay.Start();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _ticker.Stop();
        _delay.Stop();
    }

    private void Start(FloatingFade fade)
    {
        _ticker.Stop();

        // 打断重来时帧号要翻过来（FloatingDecorator.java:291-293）
        _frame = FloatingFade.FlipFrame(_frame, ForgeMetrics.FloatingFadeTotalFrames);
        _fade = fade;
        _ticker.Start();
    }

    private void Tick()
    {
        _frame++;
        CurrentRatio = _fade.RatioAt(_frame);
        _window.Opacity = 1 - CurrentRatio;

        if (_frame >= ForgeMetrics.FloatingFadeTotalFrames)
        {
            _ticker.Stop();
            _frame = 0;
            CurrentRatio = _fade.EndRatio;
            _window.Opacity = 1 - CurrentRatio;
        }
    }
}
