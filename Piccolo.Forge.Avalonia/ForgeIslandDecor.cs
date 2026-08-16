using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Piccolo.Forge.Model;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 把一块面板打扮成「岛」：圆角 + 四周留缝，缝里露出的是主窗口底色（海）。
/// </summary>
/// <remarks>
/// 对应上游 <c>IslandsUICustomization.kt:942-1029 · paintIslandAreaRaw</c>，
/// 逐条出处见 <c>docs/upstream/Islands主题.md</c>。
/// <para>
/// <b>与上游的实现差异（有意）</b>：上游是 Swing——子组件先画满整块，只能在
/// <c>paintAfterChildren</c> 里用海色**盖掉**外面一圈，再抠圆角。Avalonia 的子控件受
/// <c>Margin</c> 与圆角背景约束，那一圈**根本不会被画**，露出的就是父级的海色。
/// 视觉结果一致，少一次全尺寸重绘。<b>数字全部照抄，只换了「覆盖」→「留白」这一步。</b>
/// </para>
/// <para>
/// <b>为什么缝宽是 <c>BorderWidth / 2</c> 而不是 <c>ToolWindowInset</c></b>：上游那两个
/// 是**同一段空间的两种说法**——面板占满整个槽位，<c>Island.*.insets</c> 把内容往里推，
/// 好让随后盖上去的那一圈（<c>borderWidth / 2</c>）不糊住内容。可见的缝宽始终是后者。
/// 换成 <c>Margin</c> 之后一个值就够了，<c>insets</c> 不再需要单独表达。
/// （顺带解释了上游那处看起来不自洽的地方：编辑区 <c>insets</c> 是 2 而盖上去的是 3，
/// 于是编辑区内容实际被多切掉 1px——那是覆盖模型的副作用，裁剪模型里不存在。）
/// </para>
/// </remarks>
public static class ForgeIslandDecor
{
    /// <summary>
    /// 按配色把一个面板宿主打扮成岛；非岛形态（expUI）则还原成贴边方角。
    /// </summary>
    /// <param name="host">面板宿主。</param>
    /// <param name="palette">当前配色。</param>
    /// <param name="anchor">
    /// 停靠边——非岛形态下决定那条 1px 分隔线画在哪一侧；
    /// <see langword="null"/> 表示中间的编辑区（非岛形态下不画线）。
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> 或 <paramref name="palette"/> 为 <see langword="null"/>。</exception>
    /// <param name="metricsOverride">
    /// 宿主指定的岛几何；<see langword="null"/> 表示用 <paramref name="palette"/> 自带的（上游取值）。
    /// **配色不是岛形态时它不生效**——形态由族决定，这个参数只调几何。
    /// </param>
    /// <param name="background">
    /// 底色；<see langword="null"/> 用 <see cref="ForgePalette.Background"/>（工具窗口那档）。
    /// 编辑区要传 <see cref="ForgePalette.EditorBackground"/>。
    /// </param>
    public static void Apply(
        Border host,
        ForgePalette palette,
        ToolWindowAnchor? anchor,
        ForgeIslandMetrics? metricsOverride = null,
        Color? background = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(palette);

        // 底色默认是工具窗口那档；编辑区传 EditorBackground（它比工具窗口更白/更深）。
        host.Background = new SolidColorBrush(background ?? palette.Background);

        if ((metricsOverride ?? palette.Island) is not { } island || palette.Island is null)
        {
            // expUI：贴死、方角，边框只画朝向编辑区的那一条
            host.Margin = default;
            host.CornerRadius = default;
            host.BorderBrush = new SolidColorBrush(palette.WindowBorder);
            host.BorderThickness = anchor is { } a ? EdgeThickness(a) : default;
            return;
        }

        host.Margin = new Thickness(island.HalfBorderWidth);
        host.CornerRadius = new CornerRadius(island.CornerRadius);

        // ⚠ **必须裁**：CornerRadius 只管 Border 自己画的背景与描边，**不裁子控件**。
        // 面板内容几乎总有自己的不透明底（宿主的视图不可能都是透明的），
        // 不裁的结果是四个角被内容重新填成方的 —— 岛白做了。
        host.ClipToBounds = true;

        // 描边与岛同色（Island.borderColor ← tool-window-bg）：它不是看得见的框线，
        // 是把抗锯齿后的圆角边缘补实。别改成对比色。
        host.BorderBrush = new SolidColorBrush(palette.IslandBorder);
        host.BorderThickness = new Thickness(1);
    }

    /// <summary>非岛形态下，一块面板朝向编辑区的那一条 1px 分隔线画在哪一侧。</summary>
    private static Thickness EdgeThickness(ToolWindowAnchor anchor) => anchor switch
    {
        ToolWindowAnchor.Left => new Thickness(0, 0, 1, 0),
        ToolWindowAnchor.Right => new Thickness(1, 0, 0, 0),
        ToolWindowAnchor.Bottom => new Thickness(0, 1, 0, 0),
        _ => new Thickness(0, 0, 0, 1),
    };
}
