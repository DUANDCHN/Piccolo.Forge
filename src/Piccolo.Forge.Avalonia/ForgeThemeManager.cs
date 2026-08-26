using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 装载 Forge 主题的**唯一入口**（REQ-068）。
/// </summary>
/// <remarks>
/// 每个主题是一个自包含单元（<c>Themes/&lt;族&gt;/Theme.axaml</c> + 本族颜色 + 全部控件主题），
/// 宿主只经这一个方法装载：
/// <code>ForgeThemeManager.Apply(app, ForgeThemeFamily.Islands);</code>
/// <para>
/// <b>为什么必须是唯一入口。</b>拆成两套之后开关从一个变成两个 ——「引了哪套 Styles」与
/// 「<see cref="ForgeAppearance.Family"/> 是哪族」。两者不一致就是半旧半新：
/// 模板画的是 Islands 的圆角块，而 C# 侧按 expUI 算几何、取色。这个方法把两件事收在一处，
/// 并把装了哪族记在 <see cref="Current"/> 上，供 <see cref="AssertConsistent"/> 在 Debug 下核对。
/// </para>
/// <para>
/// <b>⚠ 换主题要重启。</b>实测（<c>ThemeSwapSpikeTests</c>）：运行时换掉
/// <see cref="Application.Styles"/> 里的单元，**已经挂在树上的控件不会重新解析 ControlTheme**
/// （翻明暗变体也不会触发）。所以设置页切换面板风格只写配置、提示重启；
/// 本方法在启动时调一次。
/// </para>
/// <para>
/// 换完之后**新建**的控件取得到新单元（同篇 spike 已钉）—— 用例要换族，
/// 必须**先 <see cref="Apply"/> 再新建控件**，顺序反了会拿到旧单元且不报错。
/// </para>
/// </remarks>
public static class ForgeThemeManager
{
    private static IForgeTheme? _installed;

    /// <summary>
    /// 当前装的是哪一族。先看本类装过的；没装过就到 <c>Application.Current.Styles</c> 里找实现了
    /// <see cref="IForgeTheme"/> 的单元（宿主在 XAML 里直接写 <c>&lt;ForgeIslandsTheme/&gt;</c> 那条路）。
    /// </summary>
    public static ForgeThemeFamily? Current
    {
        get
        {
            if (_installed is not null) return _installed.Family;
            return FindInstalled(Application.Current)?.Family;
        }
    }

    /// <summary>
    /// 装一套主题（Styles 单元），同时把 <see cref="Current"/> 记下 —— 两件事必须一起换。
    /// 已经装着同一套就什么也不做；装着别的就先摘掉。
    /// </summary>
    /// <param name="application">应用。</param>
    /// <param name="theme">主题包里的那个 Styles，如 <c>new ForgeIslandsTheme()</c>。</param>
    public static void Apply(Application application, IForgeTheme theme)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(theme);

        // 已经装着同族的一套：**不换 Styles**（换了会让已挂树的控件重新套模板 —— 本类开头那条
        // "换主题要重启"就是这个原因，实测里它甚至会当场抛"控件已经有父级"），
        // 但要把宿主这次给的**种子色接过去**。
        //
        // ⚠ 曾经这里是直接 return。种子色（AccentLight / AccentDark）加进来之后那样就错了：
        //   `Apply(app, new ForgeTheme { Family = X, AccentLight = 蓝 })` 在已装着同族主题时
        //   会被整个跳过，宿主设的主题色**一声不响地没生效**。种子色只是资源值、由
        //   {DynamicResource} 取，接过去不需要重新套模板，正好可以在不动 Styles 的前提下生效。
        var present = FindInstalled(application);
        if (present is not null && present.Family == theme.Family)
        {
            if (present is ForgeThemeBase installed && theme is ForgeThemeBase incoming)
            {
                installed.AccentLight = incoming.AccentLight;
                installed.AccentDark = incoming.AccentDark;
            }

            _installed = present;
            return;
        }

        if (present is not null)
        {
            application.Styles.Remove(present);
        }

        // ★ 插到**最前**，不是追加。★
        //
        // 基础主题按定义就该排在 Application.Styles 最前：Avalonia 的隐式 ControlTheme 与
        // 样式是后来者覆盖先来者，宿主写在 App.axaml 里的那些覆盖（改窗口背景、调按钮内边距…）
        // 必须能盖过主题，而不是反过来被主题盖掉。
        //
        // ⚠ 这里曾经是 Styles.Add（追加）。12.0.1 时本类只装**外壳**主题，外壳控件类型
        //   宿主一般不覆盖，追加看不出问题；12.1.0 起装的是**全套**（含 Window 与全部控件），
        //   追加就会盖掉宿主的覆盖 —— 实测症状：宿主把窗口设成透明以透出 Win11 云母，
        //   Forge.Window 的不透明 Background 排在后面把云母整个盖住，且**不报错**。
        //   这类"顺序错→外观悄悄变了"正是本库反复警告的那一类，不该由库自己制造。
        application.Styles.Insert(0, theme);
        _installed = theme;
    }

    private static IForgeTheme? FindInstalled(Application? application)
    {
        if (application is null) return null;
        foreach (var style in application.Styles)
        {
            if (style is IForgeTheme theme) return theme;
        }

        return null;
    }

    private static ResourceDictionary? _locale;

    /// <summary>当前装着的语言字典（给 <see cref="ForgeStrings"/> 判断要不要重读）。</summary>
    internal static object? InstalledLocale => _locale;

    /// <summary>当前装的语言（资源文件名：en-US / zh-CN / zh-Hant）；没装是 null。</summary>
    public static string? CurrentLocale { get; private set; }

    /// <summary>
    /// 装一套语言：把 <c>Locales/{name}.axaml</c> 挂进 <c>Application.Resources.MergedDictionaries</c>
    /// （照 Semi 的 <c>SemiTheme.Locale</c>）。换语言就再调一次，旧的摘掉。
    /// </summary>
    /// <remarks>
    /// 回落规则见 <see cref="ResolveLocale(string)"/>；自带语言见 <see cref="SupportedLocales"/>。
    /// 宿主想覆盖某句，在自己的资源里重定义 <c>Forge.Strings.*</c> 同名键（后加的字典优先）。
    /// </remarks>
    /// <param name="application">应用。</param>
    /// <param name="culture">要用的文化；null = <see cref="CultureInfo.CurrentUICulture"/>。</param>
    public static void ApplyLocale(Application application, CultureInfo? culture = null)
        => ApplyLocale(application, (culture ?? CultureInfo.CurrentUICulture).Name);

    /// <summary>按文化名装语言，规则同 <see cref="ApplyLocale(Application, CultureInfo?)"/>。</summary>
    public static void ApplyLocale(Application application, string cultureName)
    {
        ArgumentNullException.ThrowIfNull(application);
        var name = ResolveLocale(cultureName);
        if (name == CurrentLocale && _locale is not null && application.Resources.MergedDictionaries.Contains(_locale))
        {
            return;
        }

        if (_locale is not null)
        {
            application.Resources.MergedDictionaries.Remove(_locale);
        }

        var dictionary = Locales.ForgeLocales.Factories[name]();
        application.Resources.MergedDictionaries.Add(dictionary);
        _locale = dictionary;
        CurrentLocale = name;
    }

    /// <summary>文化 → 资源文件名。没对应的一律英文。</summary>
    public static string ResolveLocale(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return ResolveLocale(culture.Name);
    }

    /// <summary>
    /// 自带的语言字典（<c>Locales/*.axaml</c>），与 Semi.Avalonia 的语言集合对齐：
    /// 英、简中、繁中、日、韩、德、法、西、意、荷、波、俄、乌。
    /// </summary>
    public static IReadOnlyList<string> SupportedLocales { get; } = [.. Locales.ForgeLocales.Factories.Keys];

    /// <summary>文化名（如 <c>zh-TW</c>、<c>de-AT</c>）→ 资源文件名。与 <see cref="ResolveLocale(CultureInfo)"/> 同一规则，给不能造 CultureInfo 的环境（不变文化模式）用。</summary>
    /// <remarks>
    /// 按语言前缀匹配，地区不区分（<c>de-AT</c> → de-DE，<c>fr-CA</c> → fr-FR）；
    /// 中文按文字分：<c>Hant / TW / HK / MO</c> → zh-Hant，其余 → zh-CN；没对应的一律 en-US。
    /// </remarks>
    public static string ResolveLocale(string cultureName)
    {
        var n = cultureName ?? string.Empty;
        if (n.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return n.Contains("Hant", StringComparison.OrdinalIgnoreCase)
                   || n.EndsWith("-TW", StringComparison.OrdinalIgnoreCase)
                   || n.EndsWith("-HK", StringComparison.OrdinalIgnoreCase)
                   || n.EndsWith("-MO", StringComparison.OrdinalIgnoreCase)
                ? "zh-Hant"
                : "zh-CN";
        }

        var dash = n.IndexOf('-');
        var language = dash < 0 ? n : n[..dash];
        foreach (var locale in SupportedLocales)
        {
            if (locale.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase) && language.Length > 0)
            {
                return locale;
            }
        }

        return "en-US";
    }

    /// <summary>
    /// Debug 下核对「装的是哪族」与「控件的 <see cref="ForgeAppearance.Family"/>」是否一致。
    /// </summary>
    /// <remarks>
    /// 这是 REQ-068 R2（两开关漂移）的守护：有人绕过 <see cref="Apply"/> 直接设 Family，
    /// 或者设了 Family 忘了装对应单元，症状是**局部**长相不对（比如标签还是下划线、
    /// 而周围已经是圆角块），肉眼极难归因。这里在挂树时当场炸，把它变成一句可读的失败。
    /// <para>
    /// 只在 Debug 生效：发布版宁可长得不对，也不该因为主题不一致把软件打死。
    /// </para>
    /// </remarks>
    [Conditional("DEBUG")]
    public static void AssertConsistent(ForgeThemeFamily family)
    {
        if (Current is { } current && current != family)
        {
            throw new InvalidOperationException(
                $"Forge 主题不一致：装的是 {current} 那套 Styles，控件的 Appearance.Family 却是 {family}。"
                + $"两个开关必须一起换 —— 走 {nameof(ForgeThemeManager)}.{nameof(Apply)}，别单独设 Family。");
        }
    }
}
