using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
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
        frame.Appearance = frame.Appearance with { FollowThemeVariant = false };
        frame.Palette = ForgePalette.ExpUiDark;

        var window = new Window { Content = frame, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();

        Assert.Same(ForgePalette.ExpUiDark, frame.Palette);
    }

    private static ForgeControl NewFrame()
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var frame = new ForgeControl();
        frame.Attach(shell);
        return frame;
    }

    [AvaloniaFact]
    public void The_editor_area_follows_the_host_variant_too()
    {
        // 回归：编辑区没跟着变体走，于是浅色界面里中间一大片是黑的 ——
        // 它是外壳中间那块，两者跟不同主题走没有任何合理性。
        var editor = new ForgeEditorControl();
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
        var editor = new ForgeEditorControl { Palette = ForgePalette.ExpUiDark };
        editor.Attach(new EditorAreaViewModel());

        var window = new Window { Content = editor, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();

        Assert.Same(ForgePalette.ExpUiDark, editor.Palette);
    }

    /// <summary>
    /// 运行期换族要当场换配色 —— 宿主的「设置里切面板风格」走的就是这条。
    /// </summary>
    /// <remarks>
    /// 回归：换族的 handler 里曾把 <c>_variantApplied</c> 归零再调 <c>ApplyThemeVariant</c>，
    /// 而那个方法开头「宿主是不是自己管配色」的探测用 <c>IsSet(PaletteProperty)</c> 判，
    /// 在我们自己写过 Palette 之后恒为真。归零等于把探测重新武装，于是换族被判成
    /// 「宿主自己管」、<c>_paletteSetByHost</c> 被永久钉死 —— 换族没反应，
    /// 且此后连跟随系统明暗也一起停摆（本用例最后一段就是钉这个后半截的）。
    /// </remarks>
    [AvaloniaFact]
    public void Switching_the_family_at_runtime_repaints_and_keeps_following_the_variant()
    {
        var frame = NewFrame();
        var editor = new ForgeEditorControl();
        editor.Attach(new EditorAreaViewModel());
        frame.EditorContent = editor;

        var window = new Window { Content = frame, RequestedThemeVariant = ThemeVariant.Dark };
        window.Show();

        Assert.Same(ForgePalette.ExpUiDark, frame.Palette);

        // 只设外框 —— 编辑区是它的 EditorContent，外观从树上继承下来（ForgeChrome.AppearanceProperty）。
        // 早先这里要两边各设一遍，漏一边就是「四边是岛、中间还是方角」。
        frame.Appearance = frame.Appearance with { Family = ForgeThemeFamily.Islands };

        Assert.Same(ForgePalette.IslandsDark, frame.Palette);
        Assert.Same(ForgePalette.IslandsDark, editor.Palette);

        // 换过族之后，明暗仍要跟着宿主走
        window.RequestedThemeVariant = ThemeVariant.Light;

        Assert.Same(ForgePalette.IslandsLight, frame.Palette);
        Assert.Same(ForgePalette.IslandsLight, editor.Palette);

        // 换回去也要成立（不是单向的）
        frame.Appearance = frame.Appearance with { Family = ForgeThemeFamily.ExpUi };
        Assert.Same(ForgePalette.ExpUiLight, frame.Palette);
    }

    /// <summary>
    /// 宿主的真实用法：初始化器里定族，之后在设置里切。**这条是那个 bug 的最短复现。**
    /// </summary>
    /// <remarks>
    /// 初始化器那一下会真的写一次 <c>Palette</c>（族变了、值也变了），于是
    /// <c>IsSet(PaletteProperty)</c> 从此为真。旧代码在随后的换族里归零 <c>_variantApplied</c>，
    /// 探测就把这次写入误当成"宿主自己管配色"，永久钉死。
    /// 表现正是用户报的「切了没反应」。
    /// </remarks>
    [AvaloniaFact]
    public void Switching_the_family_works_even_when_it_was_set_in_the_initializer()
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var frame = new ForgeControl { Appearance = ForgeAppearance.Default with { Family = ForgeThemeFamily.Islands } };
        frame.Attach(shell);

        var window = new Window { Content = frame, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();

        Assert.Same(ForgePalette.IslandsLight, frame.Palette);

        // 设置里切成经典 —— 旧代码在这里纹丝不动
        frame.Appearance = frame.Appearance with { Family = ForgeThemeFamily.ExpUi };
        Assert.Same(ForgePalette.ExpUiLight, frame.Palette);

        // 且切过之后仍要跟着系统明暗走（旧代码这里也一起坏掉）
        window.RequestedThemeVariant = ThemeVariant.Dark;
        Assert.Same(ForgePalette.ExpUiDark, frame.Palette);
    }

    [AvaloniaFact]
    public void The_frame_and_the_editor_agree_on_the_variant()
    {
        // 宿主是分别 new 出来的两个控件，装在一起必须是同一套配色。
        var frame = NewFrame();
        var editor = new ForgeEditorControl();
        editor.Attach(new EditorAreaViewModel());
        frame.EditorContent = editor;

        var window = new Window { Content = frame, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();

        Assert.Same(frame.Palette, editor.Palette);
    }

    /// <summary>
    /// 宿主设在外框上的外观，编辑区**不用宿主再设一遍**就跟着走。
    /// </summary>
    /// <remarks>
    /// 这条钉的是接口形状本身：外观早先是两边各一份的散装属性，宿主漏设一边就得到
    /// 「四边是岛、中间还是方角」，而且加一个新属性要在四处各改一遍。
    /// 现在是一个对象 + 继承式附加属性（<see cref="ForgeChrome.AppearanceProperty"/>）。
    /// </remarks>
    [AvaloniaFact]
    public void 编辑区从外框继承整份外观()
    {
        var frame = NewFrame();
        var editor = new ForgeEditorControl();
        editor.Attach(new EditorAreaViewModel());
        frame.EditorContent = editor;

        var window = new Window { Content = frame, RequestedThemeVariant = ThemeVariant.Dark };
        window.Show();

        // 只设外框
        var metrics = ForgeIslandMetrics.Default with { BorderWidth = 3 };
        frame.Appearance = frame.Appearance with
        {
            Family = ForgeThemeFamily.Islands,
            IslandMetrics = metrics,
            TabRowHeight = 31,
        };

        Assert.Same(frame.Appearance, editor.Appearance);
        Assert.Same(ForgePalette.IslandsDark, editor.Palette);
        Assert.Equal(31, editor.Appearance.TabRowHeight);
    }

    /// <summary>
    /// 文档浮出去之后，那个窗口里的编辑区拿到的是**同一份**外观。
    /// </summary>
    /// <remarks>
    /// 回归：浮出去是**另一棵树**，继承过不来，只能显式传。早先传的是手抄的三个字段
    /// （族 / 岛几何 / 窗体工厂），抄漏了取色回调、字重、行高 ——
    /// 表现是浮窗里岛的描边没了（宿主自定义的描边色丢了，而 Islands 默认那条是全透明）、
    /// 标签字重和行高变回库默认。现在传的是整个对象，漏抄在语法上不可能。
    /// </remarks>
    [AvaloniaFact]
    public void 浮出去的编辑区拿到同一份外观()
    {
        var frame = NewFrame();
        var vm = new EditorAreaViewModel();
        var editor = new ForgeEditorControl();
        editor.Attach(vm);
        frame.EditorContent = editor;

        Window? floated = null;
        editor.ExternalWindowFactory = () => floated = new Window();

        var window = new Window { Content = frame, RequestedThemeVariant = ThemeVariant.Dark };
        window.Show();

        // 宿主自定义取色 + 字重 + 行高：早先这三样正是浮出去之后丢掉的
        var mine = ForgePalette.ForTheme(ForgeThemeFamily.Islands, ThemeVariant.Dark) with
        {
            IslandBorder = ForgeColor.Parse("#2E3237"),
        };
        frame.Appearance = frame.Appearance with
        {
            Family = ForgeThemeFamily.Islands,
            PaletteResolver = (_, _) => mine,
            TabTitleFontWeight = FontWeight.Bold,
            TabRowHeight = 31,
        };

        vm.Open("d0", "cam-1.png", new TextBlock());
        editor.MoveToNewWindow("d0");

        var floatedArea = Assert.IsType<ForgeEditorControl>(floated!.Content);
        Assert.Same(frame.Appearance, floatedArea.Appearance);
        Assert.Same(mine, floatedArea.Palette);

        floated.Close();
        window.Close();
    }
}
