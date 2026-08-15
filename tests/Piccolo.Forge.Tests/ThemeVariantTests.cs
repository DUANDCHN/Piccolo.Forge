using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 配色跟随宿主的明暗变体。
public class ThemeVariantTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("Light", false)]
    [InlineData("Dark", true)]
    public void The_palette_is_picked_by_variant(string? variantName, bool expectDark)
    {
        var variant = variantName switch
        {
            "Dark" => ThemeVariant.Dark,
            "Light" => ThemeVariant.Light,
            _ => null,
        };

        var expected = expectDark ? ForgePalette.ExpUiDark : ForgePalette.ExpUiLight;
        Assert.Same(expected, ForgePalette.ForVariant(variant));
    }

    [AvaloniaFact]
    public void The_frame_follows_the_host_variant_by_default()
    {
        // 宿主的明暗通常跟随系统；钉死一套会出现「软件变白、外壳还黑」那道硬边
        var frame = NewFrame();
        var window = new Window { Content = frame, RequestedThemeVariant = ThemeVariant.Dark };
        window.Show();

        Assert.Same(ForgePalette.ExpUiDark, frame.Palette);

        window.RequestedThemeVariant = ThemeVariant.Light;

        Assert.Same(ForgePalette.ExpUiLight, frame.Palette);
    }

    [AvaloniaFact]
    public void The_variant_reaches_the_stripe_buttons_too()
    {
        var frame = NewFrame();
        var window = new Window { Content = frame, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();

        Assert.All(
            frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>(),
            x => Assert.Same(ForgePalette.ExpUiLight, x.Palette));
    }

    [AvaloniaFact]
    public void An_explicit_palette_is_not_overwritten_by_the_variant()
    {
        // 跟随变体是默认行为，不该悄悄盖掉宿主写进去的值
        var frame = NewFrame();
        frame.Palette = ForgePalette.ExpUiDark;

        var window = new Window { Content = frame, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();

        Assert.Same(ForgePalette.ExpUiDark, frame.Palette);
    }

    [AvaloniaFact]
    public void Turning_the_switch_off_hands_control_back_to_the_host()
    {
        var frame = NewFrame();
        frame.FollowThemeVariant = false;
        frame.Palette = ForgePalette.ExpUiDark;

        var window = new Window { Content = frame, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();

        Assert.Same(ForgePalette.ExpUiDark, frame.Palette);
    }

    private static ForgeToolWindowFrame NewFrame()
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var frame = new ForgeToolWindowFrame();
        frame.Attach(shell);
        return frame;
    }
}
