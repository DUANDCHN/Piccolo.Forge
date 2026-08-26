using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 树的层级缩进只作用于**内容**，行的高亮底始终通栏。
/// </summary>
/// <remarks>
/// <b>反面（这条测试的由来）：</b>缩进曾经是 <c>ItemsPresenter</c> 上的一条 Margin
/// （<c>Forge.Tree.ChildMargin</c> = 18,0,0,0），它把整块子树连同子项的
/// <c>Border#PART_Header</c> 一起推开 —— 于是选中条每深一级就短 18，三层树累计短 36。
/// 上游不是这样：<c>com.intellij.ui.tree.ui.DefaultTreeUI.paintRowBackground</c> 按**整行**宽度
/// 画选中背景，缩进只落在内容上；Avalonia 自带的 Fluent 主题同理。
/// <para>
/// 断言分两半，缺一不可：<b>底通栏</b>（本条的目的）与<b>内容仍逐级缩进</b>
/// （防止"把 Margin 一删了事"—— 那样底是通栏了，可层级也看不出来了）。
/// </para>
/// <para>
/// ⚠ 顺带钉住 <c>MultiBinding</c> + <c>DynamicResource</c> 这条链真的解得出来：
/// 缩进量来自主题键 <c>Forge.Tree.Indent</c>，解不出来时转换器按 0 画 ——
/// 不报错，只是全树不缩进。第二半断言同时拦住这个。
/// </para>
/// </remarks>
public class TreeIndentTests
{
    [AvaloniaTheory]
    [InlineData(ForgeThemeFamily.ExpUi)]
    [InlineData(ForgeThemeFamily.Islands)]
    public void 层级缩进只推内容行的底通栏(ForgeThemeFamily family)
    {
        using var theme = ForgeThemeScope.Use(family);

        // 三层：与上位机设备树同形（传输类型 → 网口 → 相机），也是原缺陷最明显的深度。
        var leaf = new TreeViewItem { Header = "相机" };
        var middle = new TreeViewItem { Header = "网口", IsExpanded = true };
        middle.Items.Add(leaf);
        var root = new TreeViewItem { Header = "GigE Vision", IsExpanded = true };
        root.Items.Add(middle);

        var tree = new TreeView();
        tree.Items.Add(root);

        var window = new Window { Width = 320, Height = 400, Content = tree };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var indent = (double)tree.FindResource("Forge.Tree.Indent")!;
        var rows = new[] { root, middle, leaf };

        // 一半：三层的高亮底左沿与宽度完全一致 —— 选中条通栏，不逐级变短。
        var headers = rows.Select(Header).ToArray();
        foreach (var header in headers)
        {
            Assert.Equal(headers[0].Bounds.Width, header.Bounds.Width);
            Assert.Equal(LeftIn(header, tree), LeftIn(headers[0], tree));
        }

        // 另一半：内容（展开箭头）仍然逐级右移一个 Forge.Tree.Indent。
        for (var level = 0; level < rows.Length; level++)
        {
            Assert.Equal(level, rows[level].Level);
            Assert.Equal(level * indent, LeftIn(Chevron(rows[level]), Header(rows[level])) - ChevronOwnMargin);
        }
    }

    /// <summary>模板里 <c>PART_ExpandButton</c> 自带的左外边距，与层级无关。</summary>
    private const double ChevronOwnMargin = 4;

    /// <remarks>
    /// 用 <c>First</c> 而不是 <c>Single</c>：子项也是本行的视觉后代，它们各有一个同名的部件。
    /// 深度优先下本行自己的那个排在最前（模板根 StackPanel 的头一个孩子就是它）。
    /// </remarks>
    private static Border Header(TreeViewItem row)
        => row.GetVisualDescendants().OfType<Border>().First(x => x.Name == "PART_Header");

    private static Control Chevron(TreeViewItem row)
        => row.GetVisualDescendants().OfType<ToggleButton>().First(x => x.Name == "PART_ExpandButton");

    private static double LeftIn(Visual child, Visual ancestor)
        => child.TranslatePoint(default, ancestor)!.Value.X;
}
