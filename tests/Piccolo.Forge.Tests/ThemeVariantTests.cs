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

    [AvaloniaFact]
    public void The_editor_area_follows_the_host_variant_too()
    {
        // 回归：编辑区没跟着变体走，于是浅色界面里中间一大片是黑的 ——
        // 它是外壳中间那块，两者跟不同主题走没有任何合理性。
        var editor = new ForgeEditorArea();
        editor.Attach(new EditorAreaViewModel());

        var window = new Window { Content = editor, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();

        Assert.Same(ForgePalette.ExpUiLight, editor.Palette);

        window.RequestedThemeVariant = ThemeVariant.Dark;
        Assert.Same(ForgePalette.ExpUiDark, editor.Palette);
    }

    [AvaloniaFact]
    public void The_editor_area_keeps_an_explicit_palette()
    {
        var editor = new ForgeEditorArea { Palette = ForgePalette.ExpUiDark };
        editor.Attach(new EditorAreaViewModel());

        var window = new Window { Content = editor, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();

        Assert.Same(ForgePalette.ExpUiDark, editor.Palette);
    }

    [AvaloniaFact]
    public void The_frame_and_the_editor_agree_on_the_variant()
    {
        // 宿主是分别 new 出来的两个控件，装在一起必须是同一套配色。
        var frame = NewFrame();
        var editor = new ForgeEditorArea();
        editor.Attach(new EditorAreaViewModel());
        frame.EditorContent = editor;

        var window = new Window { Content = frame, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();

        Assert.Same(frame.Palette, editor.Palette);
    }
}
