using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// Alt 双击并按住 = 临时把条带浮出来。
public class StripeOverlayStateTests
{
    [Fact]
    public void The_full_gesture_takes_press_release_press()
    {
        // ToolWindowManagerImpl.kt:373-394 —— WAITING → PRESSED → RELEASED → HOLD
        var state = new StripeOverlayState();
        Assert.Equal(KeyGestureState.Waiting, state.State);

        state.ProcessModifierKey(pressed: true);
        Assert.Equal(KeyGestureState.Pressed, state.State);
        Assert.False(state.IsStripesOverlaid);

        state.ProcessModifierKey(pressed: false);
        Assert.Equal(KeyGestureState.Released, state.State);
        Assert.False(state.IsStripesOverlaid);

        state.ProcessModifierKey(pressed: true);
        Assert.Equal(KeyGestureState.Hold, state.State);
        Assert.True(state.IsStripesOverlaid);
    }

    [Fact]
    public void A_single_press_and_release_never_overlays()
    {
        var state = new StripeOverlayState();
        state.ProcessModifierKey(true);
        state.ProcessModifierKey(false);
        state.OnDoubleClickTimeout();

        Assert.Equal(KeyGestureState.Waiting, state.State);
        Assert.False(state.IsStripesOverlaid);
    }

    [Fact]
    public void The_timeout_leaves_an_established_hold_alone()
    {
        // :193-195 —— 只有不在 HOLD 才复位
        var state = Held();

        state.OnDoubleClickTimeout();

        Assert.Equal(KeyGestureState.Hold, state.State);
        Assert.True(state.IsStripesOverlaid);
    }

    [Fact]
    public void Releasing_from_hold_resets()
    {
        var state = Held();

        state.ProcessModifierKey(pressed: false);

        Assert.Equal(KeyGestureState.Waiting, state.State);
        Assert.False(state.IsStripesOverlaid);
    }

    [Fact]
    public void The_advanced_setting_switches_states_without_overlaying()
    {
        // :382-384 —— ide.suppress.double.click.handler
        var state = new StripeOverlayState { SuppressDoubleClickHandler = true };
        state.ProcessModifierKey(true);
        state.ProcessModifierKey(false);
        state.ProcessModifierKey(true);

        Assert.Equal(KeyGestureState.Hold, state.State);
        Assert.False(state.IsStripesOverlaid);
    }

    [Fact]
    public void Reset_clears_everything()
    {
        // :368-371 —— 按了别的键 / 切项目 / 窗口失焦都走这条
        var state = Held();

        state.Reset();

        Assert.Equal(KeyGestureState.Waiting, state.State);
        Assert.False(state.IsStripesOverlaid);
    }

    [Fact]
    public void The_double_click_window_is_300ms()
        => Assert.Equal(300, StripeOverlayState.DoubleClickTimeoutMs);

    private static StripeOverlayState Held()
    {
        var state = new StripeOverlayState();
        state.ProcessModifierKey(true);
        state.ProcessModifierKey(false);
        state.ProcessModifierKey(true);
        return state;
    }
}
