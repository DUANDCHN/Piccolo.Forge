using System;
using System.Diagnostics;
using Avalonia;
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
    private static IStyle? _installed;

    /// <summary>当前装着哪一族；<see langword="null"/> = 还没装过。</summary>
    /// <remarks>宿主不该按它做业务判断，它是给一致性守护与用例用的。</remarks>
    public static ForgeThemeFamily? Current { get; private set; }

    /// <summary>把 <paramref name="family"/> 那套主题装进 <paramref name="application"/>。</summary>
    /// <remarks>
    /// 原子地做两件事：卸掉上一个单元、装上这一个，并记下 <see cref="Current"/>。
    /// 重复用同一族调用是幂等的（先卸后装，不会叠两份）。
    /// <para>
    /// 装载时机：**读完配置之后、外壳控件挂树之前**。装晚了首帧没有模板 ——
    /// 不报错，只是什么都不画（REQ-067 那条「不加 include 就什么都不画」的教训）。
    /// </para>
    /// <para>
    /// ⚠ 每次都**新建**一个 <see cref="StyleInclude"/>，不复用上一次那个实例：
    /// Avalonia 的 <c>Styles</c> 认「宿主」，从 <see cref="Application.Styles"/> 摘下来的实例
    /// 再装回去会抛 <c>The Styles already has a owner</c>。缓存实例省下的那点解析开销，
    /// 换来的是切一次族就崩 —— 这条实测踩过。
    /// </para>
    /// </remarks>
    public static void Apply(Application application, ForgeThemeFamily family)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (Current == family && _installed is not null && application.Styles.Contains(_installed))
        {
            return;
        }

        if (_installed is not null)
        {
            application.Styles.Remove(_installed);
        }

        var unit = new StyleInclude((Uri?)null)
        {
            Source = new Uri($"avares://Piccolo.Forge.Avalonia/Themes/{family}/Theme.axaml"),
        };

        application.Styles.Add(unit);
        _installed = unit;
        Current = family;
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
