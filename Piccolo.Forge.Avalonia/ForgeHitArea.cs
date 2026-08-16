using Avalonia;
using Avalonia.Media;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 自绘控件的「整块都能点」。
/// </summary>
/// <remarks>
/// <b>Avalonia 的命中测试基于渲染内容</b>：控件哪块没画东西，哪块就点不到。
/// 这些控件平时（未选中、未悬停）根本不画背景，于是只有画了图标 / 文字的那一小块
/// 收得到鼠标 —— 表现为「鼠标只有经过图标才能点」，光标在按钮的其余部分毫无反应。
/// <para>
/// 填一层 <see cref="Brushes.Transparent"/> 就够：看不见，但整块参与命中测试。
/// 这是 Avalonia 里的标准做法，同一个坑 <c>ForgeDockResizer</c> 也踩过。
/// </para>
/// </remarks>
internal static class ForgeHitArea
{
    /// <summary>把整个控件铺成可命中的区域。</summary>
    /// <param name="context">绘图上下文。</param>
    /// <param name="size">控件自己的尺寸。</param>
    public static void Fill(DrawingContext context, Size size)
        => context.FillRectangle(Brushes.Transparent, new Rect(size));
}
