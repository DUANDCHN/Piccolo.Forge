using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 树的**整行**双击都能展开 / 折叠——不只是名字那一小段。
/// </summary>
/// <remarks>
/// 由来：宿主报「一整条看着都能点的行，行尾空白处双击毫无反应」。框架自带的
/// <c>TreeViewItem.OnHeaderDoubleTapped</c> 只认 <c>PART_HeaderPresenter</c> 里面的双击，
/// 而行上文字以外那片命中的是 <c>PART_Header</c> 边框。补法与实测结论见 <see cref="ForgeTree"/>。
/// <para>
/// 这几条**不是**测"属性设了没有"，而是测**真的翻转了展开态**——属性挂上了却没订阅上事件，
/// 断言属性会全绿而界面照样点不动。
/// </para>
/// </remarks>
public class ForgeTreeRowExpandTests
{
    [AvaloniaFact]
    public void 行上文字以外双击也能展开()
    {
        var (window, tree, row, theme) = Build();

        Assert.True(ForgeTree.GetRowDoubleClickExpands(tree), "主题应默认开启整行双击展开");
        Assert.False(row.IsExpanded);                       // 前提：初始是收起的，否则这条测了个寂寞
        var header = Part<Border>(row, "PART_Header");

        RaiseDoubleTapped(header);
        Assert.True(row.IsExpanded);

        RaiseDoubleTapped(header);                    // 再来一次要能收回去
        Assert.False(row.IsExpanded);

        window.Close();
        theme.Dispose();
    }

    /// <summary>
    /// 双击箭头**净效果不变**：它自己是 ToggleButton，两下各翻一次；本行为若再插一脚，
    /// 就把"双击箭头等于没点"改成了"双击箭头会展开"。
    /// </summary>
    [AvaloniaFact]
    public void 双击展开箭头不额外翻转()
    {
        var (window, tree, row, theme) = Build();
        var chevron = Part<ToggleButton>(row, "PART_ExpandButton");

        var before = row.IsExpanded;
        RaiseDoubleTapped(chevron);
        Assert.Equal(before, row.IsExpanded);

        window.Close();
        theme.Dispose();
    }

    /// <summary>叶子行没有子节点：双击在宿主那边另有语义（打开、启用…），本行为不许抢。</summary>
    [AvaloniaFact]
    public void 叶子行双击不被本行为拦下()
    {
        var (window, tree, row, theme) = Build();
        row.IsExpanded = true;
        Dispatcher.UIThread.RunJobs();

        var leaf = row.GetVisualDescendants().OfType<TreeViewItem>().First();
        Assert.Equal(0, leaf.ItemCount);                    // 前提：它确实是叶子

        var e = RaiseDoubleTapped(Part<Border>(leaf, "PART_Header"));
        Assert.False(e.Handled);                            // 没被吃掉，宿主的处理器还收得到

        window.Close();
        theme.Dispose();
    }

    /// <summary>宿主要退回框架原样时关得掉。</summary>
    [AvaloniaFact]
    public void 关掉之后整行双击不再展开()
    {
        var (window, tree, row, theme) = Build();
        ForgeTree.SetRowDoubleClickExpands(tree, false);

        RaiseDoubleTapped(Part<Border>(row, "PART_Header"));
        Assert.False(row.IsExpanded);

        window.Close();
        theme.Dispose();
    }

    /// <summary>⚠ 主题作用域必须**活到测试结束**：它一 Dispose，主题给树设的那个附加属性就跟着没了，
    /// 行为静默失效而断言"属性设了没有"照样能绿。第一版就是在这里提前 Dispose，主用例当场失败。</summary>
    private static (Window Window, TreeView Tree, TreeViewItem Row, IDisposable Theme) Build()
    {
        var theme = ForgeThemeScope.Use(ForgeThemeFamily.ExpUi);
        var tree = new TreeView
        {
            ItemsSource = new[] { new Node("分组", [new Node("子项", [])]) },
            ItemTemplate = new FuncTreeDataTemplate<Node>(
                (n, _) => new TextBlock { Text = n.Name }, n => n.Children),
        };
        var window = new Window { Width = 300, Height = 200, Content = tree, RequestedThemeVariant = ThemeVariant.Light };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
        var row = tree.GetVisualDescendants().OfType<TreeViewItem>().First();
        return (window, tree, row, theme);
    }

    private static T Part<T>(Visual host, string name) where T : Visual
        => host.GetVisualDescendants().OfType<T>().First(v => v.Name == name);

    /// <summary>
    /// 照实际路径发一次双击：**在命中元素上发**，让它冒泡到 TreeView。
    /// ⚠ 别改成在 TreeView 上发——<c>RaiseEvent</c> 会把 <c>Source</c> 换成发事件的那个对象，
    ///   于是处理器看到的是树而不是行，判不出该翻哪一行（第一版就是这么写的，探针查出来的）。
    /// </summary>
    private static RoutedEventArgs RaiseDoubleTapped(Interactive source)
    {
        var e = new TappedEventArgs(InputElement.DoubleTappedEvent, null!);
        source.RaiseEvent(e);
        Dispatcher.UIThread.RunJobs();
        return e;
    }

    private sealed record Node(string Name, Node[] Children);
}
