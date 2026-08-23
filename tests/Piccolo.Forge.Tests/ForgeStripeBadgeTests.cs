using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 条带按钮的未读角标要**钉在背板的右上角**，不是整颗按钮的右上角。
/// </summary>
/// <remarks>
/// 方底档（宿主用 ForgeAppearance 收窄按钮）的背板是"显式边长 + 居中"、
/// BackgroundInsets 为零 —— 角标按零边距贴按钮角就飘到背板外了（实机看出来的）。
/// 判据取几何包含关系（角标中心落在背板内），不取具体像素。
/// </remarks>
public class ForgeStripeBadgeTests
{
    [AvaloniaFact]
    public void 方底档_角标钉在背板角上()
    {
        var button = new ForgeStripeButton
        {
            Text = "N",
            HasBadge = true,
            HighlightInsetHorizontal = 5,
            HighlightSize = 26,   // 方底档：36 宽的按钮里居中一块 26 的方底
        };
        var window = new Window { Width = 200, Height = 200, Content = button };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var badge = button.GetVisualDescendants().OfType<Ellipse>()
            .Single(e => e.Name == "PART_Badge");
        Assert.True(badge.IsVisible);

        var plate = button.HighlightRect(button.Bounds.Size);
        var center = badge.TranslatePoint(new Point(badge.Bounds.Width / 2, badge.Bounds.Height / 2), button)!.Value;

        // 圆点中心必须落在背板矩形里（允许挑出去的是边缘那 1px，不是整颗点）。
        Assert.True(plate.Contains(center),
            $"角标中心 {center} 落在背板 {plate} 之外 —— 圆点飘出去了");
        window.Close();
    }
}
