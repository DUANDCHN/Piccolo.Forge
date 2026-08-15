namespace Piccolo.Forge.Model;

/// <summary>
/// Alt 双击并按住时临时把条带浮出来的那个状态机。
/// </summary>
/// <remarks>
/// 对应 <c>ToolWindowManagerImpl.kt:331-394 · dispatchKeyEvent / processState</c>，
/// 见 <c>工具窗口模型.md</c> §15.2。
/// <para>
/// 键盘钩子在宿主应用那边，这里只做状态迁移，好让它能不装钩子就测。
/// </para>
/// </remarks>
public sealed class StripeOverlayState
{
    /// <summary>
    /// 两次按下之间的最长间隔 300ms。
    /// </summary>
    /// <remarks>
    /// 系统属性 <c>actionSystem.keyGestureDblClickTime</c>，默认 300
    /// （<c>ToolWindowManagerImpl.kt:191-197</c>）。
    /// </remarks>
    public const int DoubleClickTimeoutMs = 300;

    /// <summary>当前状态。</summary>
    public KeyGestureState State { get; private set; } = KeyGestureState.Waiting;

    /// <summary>
    /// 条带该不该临时浮出来。
    /// </summary>
    /// <remarks>
    /// 就是上游的 <c>isStripesOverlaid</c>（<c>ToolWindowPane.kt:838-841</c>），
    /// 参与条带可见性的计算：<c>visible = showButtons || isStripesOverlaid</c>
    /// （<c>ToolWindowPaneNewButtonManager.kt:77</c>）。
    /// </remarks>
    public bool IsStripesOverlaid { get; private set; }

    /// <summary>
    /// 抑制「双击」手势的高级设置。
    /// </summary>
    /// <remarks>
    /// <c>ide.suppress.double.click.handler</c> 打开时**只切状态不浮条带**
    /// （<c>ToolWindowManagerImpl.kt:382-384</c>）。
    /// </remarks>
    public bool SuppressDoubleClickHandler { get; init; }

    /// <summary>
    /// 修饰键按下 / 松开。
    /// </summary>
    /// <remarks>对应 <c>:373-394 · processState</c>。</remarks>
    /// <param name="pressed">是按下还是松开。</param>
    public void ProcessModifierKey(bool pressed)
    {
        if (pressed)
        {
            switch (State)
            {
                case KeyGestureState.Waiting:
                    State = KeyGestureState.Pressed;
                    break;

                case KeyGestureState.Released:
                    State = KeyGestureState.Hold;
                    if (!SuppressDoubleClickHandler)
                    {
                        IsStripesOverlaid = true;
                    }

                    break;
            }

            return;
        }

        if (State == KeyGestureState.Pressed)
        {
            State = KeyGestureState.Released;
            return;
        }

        Reset();
    }

    /// <summary>
    /// 等第二次按下的定时器到点了。
    /// </summary>
    /// <remarks>
    /// 对应 <c>:191-197</c>：**没进 HOLD 才复位**，已经在 HOLD 的不动。
    /// </remarks>
    public void OnDoubleClickTimeout()
    {
        if (State != KeyGestureState.Hold)
        {
            Reset();
        }
    }

    /// <summary>
    /// 复位：按了别的键、切了项目、按着鼠标、或者窗口失焦时都走这里。
    /// </summary>
    /// <remarks>对应 <c>:368-371 · resetHoldState</c>。</remarks>
    public void Reset()
    {
        State = KeyGestureState.Waiting;
        IsStripesOverlaid = false;
    }
}

/// <summary>
/// Alt 手势的四个状态。
/// </summary>
/// <remarks>对应上游的 <c>KeyState</c>。</remarks>
public enum KeyGestureState
{
    /// <summary>什么都没发生。</summary>
    Waiting,

    /// <summary>第一次按下了。</summary>
    Pressed,

    /// <summary>第一次松开了，正在等第二次按下。</summary>
    Released,

    /// <summary>第二次按住不放 —— 条带浮出来。</summary>
    Hold,
}
