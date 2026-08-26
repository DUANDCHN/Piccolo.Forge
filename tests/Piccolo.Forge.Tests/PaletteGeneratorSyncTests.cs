using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 钉住「生成器与生成物一致」：两个 <c>Controls.Colors.axaml</c> 由
/// <c>scripts/gen-forge-palette.py</c> 生成，文件头也写了不要手改。
/// </summary>
/// <remarks>
/// 但生成物是**跑得起来的那份** —— 改它当场就能看到效果，改完忘了回填生成器，
/// 下次谁重跑脚本（上游更新时的正常操作）就把这些改动静默冲掉，且冲掉时一声不响。
/// <para>
/// 2026-08-26 清过一次实打实的漂移：按钮/输入框/下拉的 Padding、滚动条 thumb 内缩，
/// 外加生成器完全不知道的 6 个键（<c>Input.Glyph*</c>、<c>ScrollBar.ThumbHoverMargin</c>／
/// <c>ThumbRadius</c>、<c>Tab.Underline</c>、<c>Tab.UnderlineHeight</c>）。全靠人肉比对发现，
/// 所以有了这个文件。
/// </para>
/// <para>
/// ⚠ 这里**查不了色值**：色值要重跑生成器才知道，而那要 <c>third_party/intellij-community</c>
///   （不进仓，CI 上也没有）。所以只查两件不依赖上游的事 —— 度量块逐字相同、色板的键与顺序同
///   MAP。色值漂移只能靠按 <c>docs/conventions/拉上游参考.md</c> 拉上游、重跑、看 <c>git diff</c> 兜。
/// </para>
/// <para>红了怎么办：先确认生成物里那个值是对的（它是在跑的），再把它补回生成器，而不是反过来。</para>
/// </remarks>
public class PaletteGeneratorSyncTests
{
    [Theory]
    [InlineData("ExpUi")]
    [InlineData("Islands")]
    public void 度量块与生成器的METRICS逐字相同(string family)
    {
        var py = Generator();

        // METRICS 里 @TAB_UNDERLINE_HEIGHT@ 是按档展开的占位符（经典档画下划线、Islands 画药丸底）。
        var expected = Between(py, "METRICS = '''", "'''")
            .Replace("@TAB_UNDERLINE_HEIGHT@",
                     Between(Between(py, "TAB_UNDERLINE_HEIGHT = {", "\n}\n"), $"'{family}': '''", "'''"));

        // 生成器是把 METRICS 原样接在 ThemeDictionaries 之后的，取回来的就该是同一段。
        var actual = Between(Palette(family), "    </ResourceDictionary.ThemeDictionaries>\n\n", "</ResourceDictionary>\n");

        Assert.Equal(expected.Split('\n'), actual.Split('\n'));
    }

    [Theory]
    [InlineData("ExpUi", "Light")]
    [InlineData("ExpUi", "Dark")]
    [InlineData("Islands", "Light")]
    [InlineData("Islands", "Dark")]
    public void 色板的键与顺序同MAP(string family, string variant)
    {
        // MAP 条目一律以 4 空格 + ('Forge.… 起头，多行条目的续行缩进更深，不会误匹配。
        var expected = Regex.Matches(Between(Generator(), "\nMAP = [\n", "\n]\n"),
                                     @"^    \('(Forge\.[^']+)'", RegexOptions.Multiline)
                            .Select(m => m.Groups[1].Value);

        var block = Between(Palette(family), $"        <ResourceDictionary x:Key=\"{variant}\">\n",
                            "\n        </ResourceDictionary>");
        var actual = Regex.Matches(block, "<SolidColorBrush x:Key=\"([^\"]+)\"")
                          .Select(m => m.Groups[1].Value);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("ExpUi")]
    [InlineData("Islands")]
    public void 生成物带BOM(string family)
    {
        // 生成器写的是 utf-8-sig。少了这条，谁把它改回 utf-8 都不会有人发现，
        // 只会在下次重跑时于首行留一条无谓的 diff。
        var head = File.ReadAllBytes(PalettePath(family)).Take(3);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, head);
    }

    private static string Generator() => Read(Path.Combine(RepoRoot, "scripts", "gen-forge-palette.py"));

    private static string Palette(string family) => Read(PalettePath(family));

    private static string PalettePath(string family) =>
        Path.Combine(RepoRoot, "src", $"Piccolo.Forge.Themes.{family}", "Controls.Colors.axaml");

    /// <summary>读成 LF：仓里这几个文件是 CRLF，逐字比对前先抹平行尾差异。</summary>
    private static string Read(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

    private static string Between(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"找不到起点标记：{start}");
        from += start.Length;

        var to = text.IndexOf(end, from, StringComparison.Ordinal);
        Assert.True(to >= 0, $"找不到终点标记：{end}");

        return text[from..to];
    }

    private static string RepoRoot { get; } = FindRepoRoot();

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Piccolo.Forge.slnx")))
                return dir.FullName;
        }

        throw new InvalidOperationException($"从 {AppContext.BaseDirectory} 往上找不到 Piccolo.Forge.slnx");
    }
}
