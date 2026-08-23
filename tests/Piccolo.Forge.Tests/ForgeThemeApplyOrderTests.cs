using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// <see cref="ForgeThemeManager.Apply"/> 必须把主题插到 <c>Application.Styles</c> **最前**。
/// </summary>
/// <remarks>
/// ★ 这是 12.1.0 的一个真回归的防回潮用例。★
/// 那时 Apply 是 <c>Styles.Add</c>（追加）。12.0.1 只装外壳主题时看不出问题；
/// 12.1.0 起装的是全套（含 <c>Window</c> 与全部控件），追加就排在宿主自己的覆盖**之后**，
/// 于是主题反过来盖掉宿主 —— 实测症状：宿主把窗口设透明以透出 Win11 云母，
/// <c>Forge.Window</c> 的不透明 Background 把云母整个盖住，**且不报错**。
/// <para>
/// 判据取"位置"而不是"效果"：效果要真窗口 + 真合成器才看得到，无头环境测不了；
/// 而位置是因，位置对了这一类覆盖关系就都对。
/// </para>
/// </remarks>
public class ForgeThemeApplyOrderTests
{
    [AvaloniaFact]
    public void 装主题时插到最前_而不是追加()
    {
        var app = Application.Current!;

        // 宿主自己的覆盖：装主题之前就在 Styles 里（真实宿主是 App.axaml 里的 StyleInclude）
        var hostOverride = new Style(x => x.OfType<Window>());
        app.Styles.Add(hostOverride);

        ForgeThemeManager.Apply(app, new ForgeTheme { Family = ForgeThemeFamily.ExpUi });

        Assert.IsAssignableFrom<IForgeTheme>(app.Styles[0]);
        Assert.True(
            app.Styles.IndexOf(hostOverride) > 0,
            "宿主的覆盖必须排在主题之后，否则会被主题盖掉。");

        app.Styles.Remove(hostOverride);
    }

    [AvaloniaFact]
    public void 换族时仍在最前_不会越换越靠后()
    {
        var app = Application.Current!;
        var hostOverride = new Style(x => x.OfType<Window>());
        app.Styles.Add(hostOverride);

        ForgeThemeManager.Apply(app, new ForgeTheme { Family = ForgeThemeFamily.ExpUi });
        ForgeThemeManager.Apply(app, new ForgeTheme { Family = ForgeThemeFamily.Islands });

        // 换族是"摘掉旧的 + 插入新的"，插入点仍须是 0
        Assert.IsAssignableFrom<IForgeTheme>(app.Styles[0]);
        Assert.Equal(ForgeThemeFamily.Islands, ((IForgeTheme)app.Styles[0]).Family);
        Assert.Single(app.Styles, s => s is IForgeTheme);

        app.Styles.Remove(hostOverride);
    }
}
