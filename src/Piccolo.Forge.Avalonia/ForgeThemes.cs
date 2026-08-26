using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 三个主题入口（<see cref="ForgeTheme"/> / <see cref="ForgeControlsTheme"/> /
/// <see cref="ForgeShellTheme"/>）的公共底座：族由 <see cref="Family"/> 属性定，
/// 层（控件 / 外壳）由子类定。
/// </summary>
/// <remarks>
/// <para>
/// **包管"有哪些可用"，属性管"用哪个"。** 入口类都在本程序集里；族的资源（色板、外壳模板）
/// 在 Piccolo.Forge.Themes.ExpUi / .Islands 两个资源包里，按 <c>avares://</c> 在**运行期**取 ——
/// 所以选了哪族，哪族的包就必须被引用。没引用时启动即抛，异常信息里写明缺哪个包；
/// 这是本设计唯一从编译期挪到启动期的检查。
/// </para>
/// <para>
/// ⚠ <see cref="Family"/> 在启动时求值一次（XAML 字面量或 <c>{x:Static 配置}</c>），
/// **换族要重启**：已挂树的控件不会重取 ControlTheme（ThemeSwapSpikeTests 实测），
/// 运行中改属性只会换掉资源字典，外壳模板不会跟着换 —— 半旧半新比不换更糟，所以契约是重启。
/// </para>
/// </remarks>
public abstract class ForgeThemeBase : Styles, IForgeTheme
{
    private readonly bool _controls;
    private readonly bool _shell;
    private readonly List<ResourceInclude> _familyIncludes = [];
    private readonly ResourceDictionary _accent = new()
    {
        ThemeDictionaries =
        {
            [ThemeVariant.Light] = new ResourceDictionary(),
            [ThemeVariant.Dark] = new ResourceDictionary(),
        },
    };
    private ForgeThemeFamily _family;
    private Color? _accentLight;
    private Color? _accentDark;

    private protected ForgeThemeBase(bool controls, bool shell)
    {
        _controls = controls;
        _shell = shell;

        // 控件模板两档共用，与族无关，装一次就够。
        if (controls)
            Resources.MergedDictionaries.Add(Include("avares://Piccolo.Forge.Avalonia/Themes/ControlThemes.axaml"));

        // 默认经典档。这里**不验证**资源在不在：XAML 是先构造再设属性，
        // 只引了 Islands 包的宿主会先经过这里 —— 它随后设 Family="Islands" 时才验证。
        ApplyFamily(ForgeThemeFamily.ExpUi, validate: false);
    }

    /// <summary>
    /// 亮色档的**主题色种子**。不设（<c>null</c>）就用本族色板自带的强调色。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 给一支色，色板里那一批跟着强调色走的键（控件 9 个 + 外壳每族 8 个，明暗各一份）全部跟着换 ——
    /// 不必逐键覆盖。派生规则与"哪些键算强调色"见 <see cref="ForgeAccent"/>：
    /// <b>实心填充那一档取种子原值，其余保住色板原本的明度阶梯、只换色相并按种子饱和度等比缩放。</b>
    /// </para>
    /// <para>
    /// 明暗**分开给**（<see cref="AccentDark"/> 是另一支），因为这本来就是两个设计决定：
    /// 亮底上好看的色压到暗底上往往对比度不够，反过来也一样。只给一支时另一档仍用族色板的原值。
    /// </para>
    /// <para>
    /// ⚠ 派生只覆盖<b>色板</b>层。宿主若在自己的 <c>Application.Resources</c> 里又重定义了同名键，
    /// 那一处照样赢（宿主资源本来就排在 Styles 的资源之前）—— 想让某个键脱离种子单独指定时，正是靠这条。
    /// </para>
    /// <para>
    /// ⚠ 与 <see cref="Family"/> 不同，本属性**可以在运行期改**：它只换资源值，
    /// 而颜色是 <c>{DynamicResource}</c> 取的，已经挂树的控件会跟着变。
    /// （换族要重启是因为**模板**换了，那是另一回事。）
    /// </para>
    /// </remarks>
    public Color? AccentLight
    {
        get => _accentLight;
        set { _accentLight = value; RebuildAccent(); }
    }

    /// <summary>暗色档的主题色种子。说明见 <see cref="AccentLight"/>。</summary>
    public Color? AccentDark
    {
        get => _accentDark;
        set { _accentDark = value; RebuildAccent(); }
    }

    /// <summary>
    /// 这套主题的族。XAML 里写字面量（<c>Family="ExpUi"</c>）或引配置
    /// （<c>Family="{x:Static local:Settings.Family}"</c>）；启动时求值一次，换族重启生效。
    /// </summary>
    public ForgeThemeFamily Family
    {
        get => _family;
        set => ApplyFamily(value, validate: true);
    }

    // ⚠ IL2026 的由来与为什么可以压：ResourceInclude 标了 RequiresUnreferencedCode，因为运行期
    //   URI 理论上可指向任意 XAML；这里的 URI 只指向**编译进包里的 AvaloniaResource**（本程序集的
    //   ControlThemes.axaml 与两个 Themes 资源包的色板/外壳），Avalonia 资源以整块随程序集保留，
    //   不做逐成员裁剪，AOT/trim 下照常可用 —— 各示例与 LDVision 都是 AOT 发布，等于持续验证。
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "URI 只指向随包编译的 AvaloniaResource（ControlThemes / 族色板 / 外壳模板），不会被裁剪。")]
    private void ApplyFamily(ForgeThemeFamily family, bool validate)
    {
        var assembly = $"Piccolo.Forge.Themes.{family}";
        var uris = new List<Uri>();
        if (_controls)
            uris.Add(new Uri($"avares://{assembly}/Controls.Colors.axaml"));
        if (_shell)
        {
            uris.Add(new Uri($"avares://{assembly}/Colors.axaml"));
            uris.Add(new Uri($"avares://{assembly}/Shell.axaml"));
        }

        if (validate)
        {
            // 验证走 Assembly.Load 而不是 AssetLoader.Exists：后者要等 Avalonia 平台服务注册好，
            // 在 AfterSetup 回调 / 无头测试里会把"包在"误判成"不在"。而且 Themes 包是**纯资源程序集**，
            // 没有代码被谁引用，CLR 是懒加载的 —— Load 一举两得：验证包被引用了，同时把它拉进来，
            // 让后面的 avares 解析必然找得到。AOT 下只对编译进来的程序集成功，语义正好。
            try
            {
                _ = Assembly.Load(new AssemblyName(assembly));
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    $"Family={family} 需要引用包 {assembly}。引用了哪些族的包，决定了 Family 能选哪些值。", e);
            }
        }

        foreach (var include in _familyIncludes)
            Resources.MergedDictionaries.Remove(include);
        _familyIncludes.Clear();

        foreach (var uri in uris)
        {
            var include = new ResourceInclude(uri) { Source = uri };
            _familyIncludes.Add(include);
            Resources.MergedDictionaries.Add(include);
        }

        _family = family;

        // ⚠ 派生字典必须**重新排到最后**。Avalonia 查合并字典是从后往前、先命中先返回，
        //   而上面刚把族色板追加了进去 —— 不重排的话族色板排在派生之后，派生一个键也盖不住，
        //   表现成"设了种子色但颜色没变"（不报错）。构造期首次进来时它还不在表里，Remove 是空操作。
        Resources.MergedDictionaries.Remove(_accent);
        Resources.MergedDictionaries.Add(_accent);

        // 族换了，色板原值就换了，派生结果要重算（派生档的 L / S 是按族取的）。
        RebuildAccent();
    }

    /// <summary>
    /// 按当前种子色与当前族重算派生字典。种子为 <c>null</c> 的那一档留空 = 用族色板原值。
    /// </summary>
    /// <remarks>
    /// 原值经 本类的 <c>Resources</c> 现读（族色板此时已合进来），不在 C# 里存第二份色值 ——
    /// 色板改了这里自动跟着走。读不到的键跳过：只装了一层的宿主
    /// （<see cref="ForgeControlsTheme"/> / <see cref="ForgeShellTheme"/>）本来就只有一半键在。
    /// </remarks>
    private void RebuildAccent()
    {
        Fill(ThemeVariant.Light, _accentLight);
        Fill(ThemeVariant.Dark, _accentDark);

        void Fill(ThemeVariant variant, Color? seed)
        {
            var target = (ResourceDictionary)_accent.ThemeDictionaries[variant];
            target.Clear();
            if (seed is not { } value) return;

            foreach (var (key, derived) in ForgeAccent.Derive(Lookup, _family, value, _controls, _shell))
                target[key] = derived;

            Color? Lookup(string key)
            {
                // ⚠ 必须绕开 _accent 自己，否则第二次重算读到的是上一次的派生结果，
                //   种子一改再改就会**逐次漂移**（拿派生值再派生一次）。
                foreach (var dictionary in _familyIncludes)
                    if (dictionary.TryGetResource(key, variant, out var found))
                        return found switch
                        {
                            Color color => color,
                            ISolidColorBrush brush => brush.Color,
                            _ => null,
                        };

                return null;
            }
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "同 ApplyFamily：URI 只指向随包编译的 AvaloniaResource。")]
    private static ResourceInclude Include(string uri)
    {
        var u = new Uri(uri);
        return new ResourceInclude(u) { Source = u };
    }
}

/// <summary>
/// 全套主题：控件（Avalonia 全部 89 个 + 宿主壳一族）＋ 外壳（条带 / 停靠区 / 工具窗 / 编辑器标签）。
/// 一行装完，对位 SemiTheme / FluentTheme。
/// </summary>
/// <remarks>
/// <code lang="xml">
/// &lt;Application.Styles&gt;
///     &lt;forge:ForgeTheme Family="ExpUi" /&gt;
/// &lt;/Application.Styles&gt;
/// </code>
/// 只要其中一层的，见 <see cref="ForgeControlsTheme"/> / <see cref="ForgeShellTheme"/> ——
/// 两层的 ControlTheme 目标类型不相交，两个入口可以并存；但**别**把本入口与它们混装，
/// 重复的那层会抢同一批隐式 ControlTheme，谁赢取决于先后，且不报错。
/// </remarks>
public sealed class ForgeTheme : ForgeThemeBase
{
    /// <summary>建一套全套主题，族默认经典（ExpUi），用 <see cref="ForgeThemeBase.Family"/> 换。</summary>
    public ForgeTheme() : base(controls: true, shell: true) { }
}

/// <summary>
/// 只有**控件**（Avalonia 全部 89 个 + 宿主壳一族）与它们的色板，不含停靠外壳。
/// 给"想要 IntelliJ 控件外观、不用 Forge 停靠框架"的宿主 —— 它本身是完整基础主题，
/// 自身即是完整基础主题，不要再叠别的基础主题。
/// </summary>
/// <remarks>
/// DataGrid / ColorPicker 的模板在附加包里（那两个控件在独立的 Avalonia 包里），
/// 用得到再挂 ForgeDataGridTheme / ForgeColorPickerTheme，且排在本入口之后。
/// </remarks>
public sealed class ForgeControlsTheme : ForgeThemeBase
{
    /// <summary>建一套只含控件的主题，族默认经典（ExpUi），用 <see cref="ForgeThemeBase.Family"/> 换。</summary>
    public ForgeControlsTheme() : base(controls: true, shell: false) { }
}

/// <summary>
/// 只有**外壳**（条带 / 停靠区 / 工具窗 / 编辑器标签）与它的色板，不含控件模板。
/// 给"已有自己的基础主题、只想要 IntelliJ 停靠"的宿主。
/// </summary>
/// <remarks>
/// ⚠ 这时按钮、输入框等由宿主的基础主题画，与外壳不同源，观感会有出入 ——
/// 这是取舍本身，不是缺陷；要观感统一用 <see cref="ForgeTheme"/>。
/// 基础主题必须排在本入口**之前**。
/// </remarks>
public sealed class ForgeShellTheme : ForgeThemeBase
{
    /// <summary>建一套只含外壳的主题，族默认经典（ExpUi），用 <see cref="ForgeThemeBase.Family"/> 换。</summary>
    public ForgeShellTheme() : base(controls: false, shell: true) { }
}
