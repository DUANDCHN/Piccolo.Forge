using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// REQ-067 的探路用例：模板化之前先钉死"资源到底取不取得到"。
/// </summary>
/// <remarks>
/// 这几条不测 Forge 的任何东西，测的是 <b>Avalonia 的资源系统在 headless 用例里的行为</b>——
/// 它决定 REQ-067 的用例怎么写，也决定控件能不能在没挂进树的时候取色。
/// 结论一旦确立就不该再动这个文件，它是那批用例的地基。
/// </remarks>
public class ThemeResourceSpikeTests
{
    private const string Key = "Forge.Spike.IslandBorder";

    /// <summary>
    /// 挂进树的控件：<c>ThemeDictionaries</c> 里的键取得到，且**跟着变体走**。
    /// </summary>
    /// <remarks>这是 REQ-067 想要的核心行为：一处覆盖，明暗各取各的。</remarks>
    [AvaloniaFact]
    public void 挂进树之后按变体取到不同的值()
    {
        var window = NewWindow(ThemeVariant.Dark);
        var probe = new Border();
        window.Content = probe;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(probe.TryFindResource(Key, probe.ActualThemeVariant, out var dark));
        Assert.Equal(Color.Parse("#2E3237"), dark);

        window.RequestedThemeVariant = ThemeVariant.Light;
        Dispatcher.UIThread.RunJobs();

        Assert.True(probe.TryFindResource(Key, probe.ActualThemeVariant, out var light));
        Assert.Equal(Color.Parse("#DFE1E5"), light);

        window.Close();
    }

    /// <summary>
    /// <b>没挂进树的控件取不到。</b>
    /// </summary>
    /// <remarks>
    /// 这条是给 REQ-067 立规矩用的：控件不能在构造里取色，只能在
    /// <c>OnAttachedToVisualTree</c> / 变体变化之后取；用例也必须先 <c>Show</c> 再断言。
    /// 早先那种 <c>new ForgeStripeButton(); button.Measure(...)</c> 的写法在模板化之后
    /// 取到的是兜底值，不是主题值 —— 写用例时要有意识。
    /// </remarks>
    [AvaloniaFact]
    public void 没挂进树就取不到()
    {
        var orphan = new Border();
        Assert.False(orphan.TryFindResource(Key, orphan.ActualThemeVariant, out _));
    }

    // ⚠ 「DynamicResource 会自己跟着变体走」这条**故意不在这里测**：
    //   它的 C# 构造方式在 Avalonia 12 变过（Avalonia.Data.IBinding 已不是独立类型），
    //   而真模板是用 XAML 写的，绕 C# API 去验等于验了一条我们不会走的路。
    //   那条由 REQ-067 的第一个真模板（ForgeStripeButton 切片）来钉。

    /// <summary>建一个带 ThemeDictionaries 的窗口 —— 模拟宿主那份 ThemeColor.axaml。</summary>
    private static Window NewWindow(ThemeVariant variant)
    {
        var window = new Window { Width = 200, Height = 100, RequestedThemeVariant = variant };
        window.Resources.ThemeDictionaries[ThemeVariant.Dark] = new ResourceDictionary
        {
            [Key] = Color.Parse("#2E3237"),
        };
        window.Resources.ThemeDictionaries[ThemeVariant.Light] = new ResourceDictionary
        {
            [Key] = Color.Parse("#DFE1E5"),
        };

        return window;
    }
}
