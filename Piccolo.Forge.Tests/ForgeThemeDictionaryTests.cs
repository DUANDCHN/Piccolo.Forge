using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 库自带的主题字典里，每个值都必须等于 <see cref="ForgePalette"/> 里对应的字段。
/// </summary>
/// <remarks>
/// <b>这是 REQ-067 整个迁移期间唯一能自动发现转写错误的东西</b>，所以它先于第一份主题字典建立。
/// <para>
/// 要防的不是笔误，是一个**会静默错色**的差异：上游与 <see cref="ForgeColor.Parse"/> 是
/// <c>#RRGGBBAA</c>（alpha 在后），Avalonia 的 <c>Color</c> 是 <c>#AARRGGBB</c>（alpha 在前），
/// **顺序相反**。把 <c>ButtonHoverBackground</c> 的 <c>#0000001D</c>（黑、11% 不透明）
/// 原样抄进 XAML，会被读成 <c>alpha=0</c> —— 悬停高亮整个消失，**不崩、不报错**，
/// 只是在某个状态里悄悄没了。
/// </para>
/// <para>
/// 要搬的颜色是 4 套配色 × 几十个字段，人眼校对必漏。加一个键就在下面的表里加一行，
/// 漏加只会少测一个键；改错值则当场红。
/// </para>
/// </remarks>
public class ForgeThemeDictionaryTests
{
    /// <summary>键名后缀 → 从一份配色里取哪个字段。</summary>
    /// <remarks>
    /// ★ 这是**颜色键的单一真相**：<see cref="ForgeThemeCompletenessTests"/> 也从这里取清单，
    /// 断言每套主题都给每个键给了值（REQ-068 R3：不许出现第二份手工清单）。
    /// 加一个键就在这里加一行。
    /// </remarks>
    internal static readonly (string Suffix, Func<ForgePalette, Color> Field)[] Entries =
    [
        ("IslandBorder", p => p.IslandBorder),
        ("ButtonForeground", p => p.ButtonForeground),
        ("ButtonSelectedForeground", p => p.ButtonSelectedForeground),
        ("ButtonSelectedBackground", p => p.ButtonSelectedBackground),
        ("ButtonPressedBackground", p => p.ButtonPressedBackground),
        ("ButtonHoverBackground", p => p.ButtonHoverBackground),
        ("StripeSeparator", p => p.StripeSeparator),
        ("StripeSeparatorDragAndDrop", p => p.StripeSeparatorDragAndDrop),
        ("StripeBackground", p => p.StripeBackground),
        ("StripeBorder", p => p.StripeBorder),
        ("ButtonDropBackground", p => p.ButtonDropBackground),
        ("ButtonDropBorder", p => p.ButtonDropBorder),
        ("DragAreaBackground", p => p.DragAreaBackground),
        ("Foreground", p => p.Foreground),
        ("HeaderBackground", p => p.HeaderBackground),
        ("HeaderBorder", p => p.HeaderBorder),
        ("HeaderTabHoverBackground", p => p.HeaderTabHoverBackground),
        ("HeaderTabUnderline", p => p.HeaderTabUnderline),
        ("HeaderTabInactiveUnderline", p => p.HeaderTabInactiveUnderline),
        ("EditorBackground", p => p.EditorBackground),
        ("EditorTabsBorder", p => p.EditorTabsBorder),
        ("EditorTabHoverBackground", p => p.EditorTabHoverBackground),
        ("EditorTabSelectedBackground", p => p.EditorTabSelectedBackground),
        ("EditorTabSelectedBorder", p => p.EditorTabSelectedBorder),
        ("EditorTabInactiveSelectedBackground", p => p.EditorTabInactiveSelectedBackground),
        ("EditorTabInactiveSelectedBorder", p => p.EditorTabInactiveSelectedBorder),
    ];

    [AvaloniaTheory]
    [InlineData("ExpUi", ForgeThemeFamily.ExpUi, false)]
    [InlineData("ExpUi", ForgeThemeFamily.ExpUi, true)]
    [InlineData("Islands", ForgeThemeFamily.Islands, false)]
    [InlineData("Islands", ForgeThemeFamily.Islands, true)]
    public void 主题字典里的每个颜色都等于对应配色的字段(string prefix, ForgeThemeFamily family, bool dark)
    {
        var variant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var (window, probe) = Build(variant, family);
        var palette = ForgePalette.ForTheme(family, variant);

        var missing = new List<string>();
        var wrong = new List<string>();

        foreach (var (suffix, field) in Entries)
        {
            var key = $"Forge.{prefix}.{suffix}";
            if (!probe.TryFindResource(key, probe.ActualThemeVariant, out var value) || value is not Color actual)
            {
                missing.Add(key);
                continue;
            }

            var expected = field(palette);
            if (actual != expected)
            {
                // 把两边都印出来：**A 通道不一致基本就是字节序抄反了**
                wrong.Add($"{key}: 字典={Describe(actual)} 配色={Describe(expected)}");
            }
        }

        Assert.Empty(missing);
        Assert.Empty(wrong);

        window.Close();
    }

    /// <summary>
    /// 宿主重定义同名键就盖过库自带的，且**明暗各盖各的**。
    /// </summary>
    /// <remarks>REQ-067 验收第 2 条。这正是「覆盖」与「跟随明暗」不再二选一的证明。</remarks>
    [AvaloniaFact]
    public void 宿主覆盖同名键并且明暗各走各的()
    {
        var (window, probe) = Build(ThemeVariant.Dark, ForgeThemeFamily.Islands);

        // 宿主那份：只重定义一个键，两个变体给不同的值
        window.Resources.ThemeDictionaries[ThemeVariant.Dark] = new ResourceDictionary
        {
            ["Forge.Islands.ButtonHoverBackground"] = Color.Parse("#FF00FF00"),
        };
        window.Resources.ThemeDictionaries[ThemeVariant.Light] = new ResourceDictionary
        {
            ["Forge.Islands.ButtonHoverBackground"] = Color.Parse("#FFFF0000"),
        };

        Dispatcher.UIThread.RunJobs();
        Assert.True(probe.TryFindResource("Forge.Islands.ButtonHoverBackground", probe.ActualThemeVariant, out var inDark));
        Assert.Equal(Color.Parse("#FF00FF00"), inDark);

        window.RequestedThemeVariant = ThemeVariant.Light;
        Dispatcher.UIThread.RunJobs();
        Assert.True(probe.TryFindResource("Forge.Islands.ButtonHoverBackground", probe.ActualThemeVariant, out var inLight));
        Assert.Equal(Color.Parse("#FFFF0000"), inLight);

        // 没被覆盖的键仍是库自带的值
        Assert.True(probe.TryFindResource("Forge.Islands.ButtonForeground", probe.ActualThemeVariant, out var untouched));
        Assert.Equal(ForgePalette.IslandsLight.ButtonForeground, untouched);

        window.Close();
    }

    private static string Describe(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}(AARRGGBB)";

    /// <summary>建一个挂了某一族颜色字典的窗口（REQ-068 之后一族一份）。</summary>
    /// <remarks>
    /// ⚠ 必须 <c>Show()</c> 之后才查得到资源——控件没挂进可视树时 <c>TryFindResource</c> 取不到，
    /// 见 <see cref="ThemeResourceSpikeTests"/>。
    /// </remarks>
    private static (Window Window, Border Probe) Build(ThemeVariant variant, ForgeThemeFamily family)
    {
        var window = new Window { Width = 200, Height = 100, RequestedThemeVariant = variant };
        window.Styles.Add(new global::Avalonia.Styling.Styles
        {
            Resources = new ResourceDictionary
            {
                MergedDictionaries =
                {
                    new ResourceInclude((Uri?)null)
                    {
                        Source = new Uri($"avares://Piccolo.Forge.Themes.{family}/Colors.axaml"),
                    },
                },
            },
        });

        var probe = new Border();
        window.Content = probe;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, probe);
    }
}
