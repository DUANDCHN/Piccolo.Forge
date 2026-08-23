using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia.Markup.Xaml.Styling;
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
    private ForgeThemeFamily _family;

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
/// 直接替掉 Fluent / Semi，不要再叠别的基础主题。
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
