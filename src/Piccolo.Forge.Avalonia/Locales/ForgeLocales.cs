using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Piccolo.Forge.Avalonia.Locales;

/// <summary>
/// 自带语言字典的工厂表。每份 <c>Locales/*.axaml</c> 都带 <c>x:Class</c>，由编译期 XAML 生成——
/// 不走 <c>ResourceInclude(Uri)</c> 那条运行时反射加载的路（它标了 RequiresUnreferencedCode，裁剪 / AOT 下会断）。
/// </summary>
internal static class ForgeLocales
{
    /// <summary>资源文件名 → 字典工厂；键集即 <see cref="ForgeThemeManager.SupportedLocales"/>。</summary>
    internal static readonly IReadOnlyDictionary<string, Func<ResourceDictionary>> Factories = new Dictionary<string, Func<ResourceDictionary>>(StringComparer.OrdinalIgnoreCase)
    {
        ["en-US"] = static () => new ForgeLocale_en_US(),
        ["zh-CN"] = static () => new ForgeLocale_zh_CN(),
        ["zh-Hant"] = static () => new ForgeLocale_zh_Hant(),
        ["ja-JP"] = static () => new ForgeLocale_ja_JP(),
        ["ko-KR"] = static () => new ForgeLocale_ko_KR(),
        ["de-DE"] = static () => new ForgeLocale_de_DE(),
        ["fr-FR"] = static () => new ForgeLocale_fr_FR(),
        ["es-ES"] = static () => new ForgeLocale_es_ES(),
        ["it-IT"] = static () => new ForgeLocale_it_IT(),
        ["nl-NL"] = static () => new ForgeLocale_nl_NL(),
        ["pl-PL"] = static () => new ForgeLocale_pl_PL(),
        ["ru-RU"] = static () => new ForgeLocale_ru_RU(),
        ["uk-UA"] = static () => new ForgeLocale_uk_UA(),
    };
}

/// <summary>en-US 文案字典（编译期 XAML，AOT / 裁剪安全）。</summary>
internal sealed class ForgeLocale_en_US : ResourceDictionary
{
    /// <summary>装载 XAML。</summary>
    public ForgeLocale_en_US() => AvaloniaXamlLoader.Load(this);
}

/// <summary>zh-CN 文案字典（编译期 XAML，AOT / 裁剪安全）。</summary>
internal sealed class ForgeLocale_zh_CN : ResourceDictionary
{
    /// <summary>装载 XAML。</summary>
    public ForgeLocale_zh_CN() => AvaloniaXamlLoader.Load(this);
}

/// <summary>zh-Hant 文案字典（编译期 XAML，AOT / 裁剪安全）。</summary>
internal sealed class ForgeLocale_zh_Hant : ResourceDictionary
{
    /// <summary>装载 XAML。</summary>
    public ForgeLocale_zh_Hant() => AvaloniaXamlLoader.Load(this);
}

/// <summary>ja-JP 文案字典（编译期 XAML，AOT / 裁剪安全）。</summary>
internal sealed class ForgeLocale_ja_JP : ResourceDictionary
{
    /// <summary>装载 XAML。</summary>
    public ForgeLocale_ja_JP() => AvaloniaXamlLoader.Load(this);
}

/// <summary>ko-KR 文案字典（编译期 XAML，AOT / 裁剪安全）。</summary>
internal sealed class ForgeLocale_ko_KR : ResourceDictionary
{
    /// <summary>装载 XAML。</summary>
    public ForgeLocale_ko_KR() => AvaloniaXamlLoader.Load(this);
}

/// <summary>de-DE 文案字典（编译期 XAML，AOT / 裁剪安全）。</summary>
internal sealed class ForgeLocale_de_DE : ResourceDictionary
{
    /// <summary>装载 XAML。</summary>
    public ForgeLocale_de_DE() => AvaloniaXamlLoader.Load(this);
}

/// <summary>fr-FR 文案字典（编译期 XAML，AOT / 裁剪安全）。</summary>
internal sealed class ForgeLocale_fr_FR : ResourceDictionary
{
    /// <summary>装载 XAML。</summary>
    public ForgeLocale_fr_FR() => AvaloniaXamlLoader.Load(this);
}

/// <summary>es-ES 文案字典（编译期 XAML，AOT / 裁剪安全）。</summary>
internal sealed class ForgeLocale_es_ES : ResourceDictionary
{
    /// <summary>装载 XAML。</summary>
    public ForgeLocale_es_ES() => AvaloniaXamlLoader.Load(this);
}

/// <summary>it-IT 文案字典（编译期 XAML，AOT / 裁剪安全）。</summary>
internal sealed class ForgeLocale_it_IT : ResourceDictionary
{
    /// <summary>装载 XAML。</summary>
    public ForgeLocale_it_IT() => AvaloniaXamlLoader.Load(this);
}

/// <summary>nl-NL 文案字典（编译期 XAML，AOT / 裁剪安全）。</summary>
internal sealed class ForgeLocale_nl_NL : ResourceDictionary
{
    /// <summary>装载 XAML。</summary>
    public ForgeLocale_nl_NL() => AvaloniaXamlLoader.Load(this);
}

/// <summary>pl-PL 文案字典（编译期 XAML，AOT / 裁剪安全）。</summary>
internal sealed class ForgeLocale_pl_PL : ResourceDictionary
{
    /// <summary>装载 XAML。</summary>
    public ForgeLocale_pl_PL() => AvaloniaXamlLoader.Load(this);
}

/// <summary>ru-RU 文案字典（编译期 XAML，AOT / 裁剪安全）。</summary>
internal sealed class ForgeLocale_ru_RU : ResourceDictionary
{
    /// <summary>装载 XAML。</summary>
    public ForgeLocale_ru_RU() => AvaloniaXamlLoader.Load(this);
}

/// <summary>uk-UA 文案字典（编译期 XAML，AOT / 裁剪安全）。</summary>
internal sealed class ForgeLocale_uk_UA : ResourceDictionary
{
    /// <summary>装载 XAML。</summary>
    public ForgeLocale_uk_UA() => AvaloniaXamlLoader.Load(this);
}
