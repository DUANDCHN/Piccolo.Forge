using System;
using System.Collections.Generic;
using System.Linq;
using global::Avalonia.Input;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 把 <see cref="KeyGesture"/> 渲染成**键帽上印的样子**：<c>Ctrl+OemComma</c> → <c>Ctrl+,</c>。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么需要。</b><see cref="KeyGesture.ToString()"/> 直接吐 <see cref="Key"/> 的枚举名，于是菜单里
/// 写着 <c>Ctrl+OemComma</c>——"Oem" 是 Windows 键盘扫描码的历史包袱，与用户手指按下的那颗键
/// 毫无关系，谁也认不出那是逗号。上游不是这样：IntelliJ 的
/// <c>com.intellij.openapi.keymap.KeymapUtil.getKeystrokeText</c> 把 <c>KeyEvent.VK_COMMA</c> 之类
/// 逐个映射成字符再拼进菜单文本，菜单里显示的是 <c>Ctrl+,</c>。
/// ⚠ <b>上游行号待补</b>：本机没有 <c>third_party/intellij-community</c> 克隆（拉法见
/// <c>docs/conventions/拉上游参考.md</c>）。类名与方法名有据，行号是欠着的账，与
/// <c>ForgeToolWindowHeader.SetTitleAdornment</c> 那笔同性质。
/// </para>
/// <para>
/// <b>只收"名字认不出那颗键"的。</b>字母、数字、F 键的枚举名本来就等于键帽，进表只会招来
/// "我按的明明是 A，它显示成别的"。表里是 Oem 系列（符号键）、小键盘运算键，以及几个
/// 约定俗成的缩写。
/// </para>
/// <para>
/// 公开而非 internal：宿主自己的快捷键录入 / 展示控件要和菜单显示成同一个样子，
/// 否则设置页写 <c>Ctrl+,</c>、菜单写 <c>Ctrl+OemComma</c>，用户会以为是两个快捷键。
/// </para>
/// </remarks>
public static class ForgeKeyGestureText
{
    private static readonly Dictionary<string, string> Display = new(StringComparer.Ordinal)
    {
        // ── Oem 系列：按的是符号键，名字却是扫描码别名 ──
        ["OemComma"] = ",",
        ["OemPeriod"] = ".",
        ["OemMinus"] = "-",
        ["OemPlus"] = "+",
        ["OemQuestion"] = "/",
        ["OemTilde"] = "`",
        ["OemOpenBrackets"] = "[",
        ["OemCloseBrackets"] = "]",
        ["OemPipe"] = "\\",
        ["OemBackslash"] = "\\",
        ["OemSemicolon"] = ";",
        ["OemQuotes"] = "'",
        // Oem1…Oem7 与上面几个在 Key 枚举里同值互为别名，ToString 给哪一个取决于声明顺序；
        // 两种拼法都收，免得换个 Avalonia 版本就漏掉一半。
        ["Oem1"] = ";",
        ["Oem2"] = "/",
        ["Oem3"] = "`",
        ["Oem4"] = "[",
        ["Oem5"] = "\\",
        ["Oem6"] = "]",
        ["Oem7"] = "'",

        // ── 小键盘运算键。带 Num 前缀是为了与主键盘上的 + - 分开：两者绑成不同手势是合法的。 ──
        ["Add"] = "Num+",
        ["Subtract"] = "Num-",
        ["Multiply"] = "Num*",
        ["Divide"] = "Num/",
        ["Decimal"] = "Num.",

        // ── 约定俗成的缩写：键帽上印的就是这些 ──
        ["Escape"] = "Esc",
        ["Return"] = "Enter",
        ["Back"] = "Backspace",
        ["Delete"] = "Del",
        ["Insert"] = "Ins",
        ["PageUp"] = "PgUp",
        ["PageDown"] = "PgDn",
    };

    /// <summary>渲染一个手势；<see langword="null"/> 得空串。</summary>
    public static string ToDisplay(KeyGesture? gesture)
        => gesture is null ? string.Empty : ToDisplay(gesture.ToString());

    /// <summary>
    /// 渲染手势的**字符串形**（<c>KeyGesture.ToString()</c> 的产物，或宿主自己存的同形规范串）。
    /// 修饰键原样保留，只换最后那颗键的名字。
    /// </summary>
    public static string ToDisplay(string? gesture)
    {
        if (string.IsNullOrEmpty(gesture)) return string.Empty;

        // 手势串里 '+' 只做分隔符：加号那颗键写作 OemPlus / Add，不会出现裸的 '+'，故直接切安全。
        return string.Join('+', gesture.Split('+').Select(part =>
            Display.TryGetValue(part, out var shown) ? shown : part));
    }
}
