using System;
using Avalonia;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 用例里临时换一族主题，出了 <c>using</c> 换回默认（REQ-068）。
/// </summary>
/// <remarks>
/// <b>为什么用例换族要这么麻烦。</b>拆成两套完整主题之后，"当前是哪族"是
/// <see cref="Application"/> 级的状态（装了哪个 Styles 单元），headless 用例又共用同一个
/// <see cref="Application"/>。不还原的话，跑过 Islands 的那条会把后面的用例一起染成 Islands，
/// 表现为"单跑绿、全跑红"这种最难查的失败。
/// <para>
/// ⚠ <b>顺序：先 <c>Use</c>，再新建控件。</b>换掉单元**不会**让已经挂在树上的控件重取
/// ControlTheme（实测见 <see cref="ThemeSwapSpikeTests"/>），所以先建控件再换族，
/// 拿到的还是旧那套 —— 而且不报错，只是断言莫名其妙地不成立。
/// </para>
/// </remarks>
public sealed class ForgeThemeScope : IDisposable
{
    /// <summary>默认那族 —— <see cref="TestAppBuilder"/> 启动时装的就是它。</summary>
    public const ForgeThemeFamily Default = ForgeThemeFamily.ExpUi;

    private ForgeThemeScope()
    {
    }

    /// <summary>换到 <paramref name="family"/>，出了作用域换回 <see cref="Default"/>。</summary>
    public static ForgeThemeScope Use(ForgeThemeFamily family)
    {
        ForgeThemeManager.Apply(Application.Current!, ThemeFor(family));
        return new ForgeThemeScope();
    }

    /// <summary>作用域内再换一族（用例里来回切族时用）。</summary>
    /// <remarks>
    /// 换族之后**已挂树的控件长相不会跟着变**（模板认死了），所以这个方法只对两种用例有意义：
    /// 换完再新建控件，或者只看 C# 侧的配色传播（<c>Palette</c>）。
    /// 它同时把 <see cref="ForgeThemeManager.Current"/> 一起换掉，
    /// 免得 <see cref="ForgeThemeManager.AssertConsistent"/> 因为"两个开关不同族"当场炸 ——
    /// 那个断言正是要拦住产品代码里绕过单一入口的写法。
    /// </remarks>
    public void SwitchTo(ForgeThemeFamily family) => ForgeThemeManager.Apply(Application.Current!, ThemeFor(family));

    /// <inheritdoc />
    public void Dispose() => ForgeThemeManager.Apply(Application.Current!, ThemeFor(Default));

    /// <summary>族 → 主题包里的 Styles。两个包都引进来了，测试要两套都能跑。</summary>
    public static IForgeTheme ThemeFor(ForgeThemeFamily family) => family switch
    {
        ForgeThemeFamily.Islands => new Piccolo.Forge.Themes.Islands.ForgeIslandsTheme(),
        _ => new Piccolo.Forge.Themes.ExpUi.ForgeExpUiTheme(),
    };
}
