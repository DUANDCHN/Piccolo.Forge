using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.VisualTree;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 树控件的 Forge 行为扩展。目前只有一条：<b>整行双击都能展开 / 折叠</b>。
/// </summary>
/// <remarks>
/// <para>
/// <b>症状。</b>一整条看着都能点的行，实际只有名字那一小段双击才展开，行尾空白处双击毫无反应。
/// </para>
/// <para>
/// <b>根因（宿主侧无头探针实测，不是推断）。</b>框架自带的双击展开是
/// <c>Avalonia.Controls.TreeViewItem.OnHeaderDoubleTapped</c>，它只认
/// <c>PART_HeaderPresenter</c> <b>里面</b>的双击；而行上文字以外那片命中的是模板里那层
/// <c>PART_Header</c> 边框（Forge 的模板给它铺了 Transparent 底，整行都可点），事件根本不经过呈现器：
/// </para>
/// <list type="bullet">
/// <item>点在文字上：<c>IsExpanded</c> 翻转，且事件被<b>标记已处理</b>，冒不到 TreeView；</item>
/// <item>点在文字以外：<c>IsExpanded</c> 不动，事件<b>原样冒泡</b>到 TreeView。</item>
/// </list>
/// <para>
/// <b>于是补法只有一句话</b>：在 TreeView 上挂一个普通的（不收已处理事件的）<c>DoubleTapped</c>
/// 处理器。框架管了的那次根本到不了这里，到得了这里的正好是它漏掉的那些——不会重复翻转，
/// 也不必去猜哪次该管哪次不该管。
/// </para>
/// <para>
/// ⚠ <b>别改成"把 <c>PART_HeaderPresenter</c> 设成不可命中、让整行统一落到边框上"</b>：
/// 那样确实整行一致了，但框架那条双击展开会<b>连文字上也一起失效</b>（同一探针验过），
/// 等于把展开整个交给自己实现，得不偿失。
/// </para>
/// <para>
/// <b>为什么在库里而不是各宿主自己补。</b>这条最早是宿主（LDVision）自己写了个
/// <c>TreeRowExpand</c> 补的——但"行看着能点、点了没反应"是**控件默认行为不对**，
/// 不是某个应用的特殊需求；每个用 Forge 树的宿主都要各修一遍才对得上。
/// 故收进 <c>Forge.TreeView</c> 主题默认开启，宿主什么都不用做。
/// </para>
/// </remarks>
public class ForgeTree
{
    /// <summary>
    /// 整行双击是否展开 / 折叠该行。<c>Forge.TreeView</c> 主题默认开启；
    /// 宿主要恢复框架原样（只有文字上双击有效）时设 <see langword="false"/>。
    /// </summary>
    public static readonly AttachedProperty<bool> RowDoubleClickExpandsProperty =
        AvaloniaProperty.RegisterAttached<ForgeTree, TreeView, bool>("RowDoubleClickExpands");

    static ForgeTree() => RowDoubleClickExpandsProperty.Changed.AddClassHandler<TreeView>(OnRowDoubleClickExpandsChanged);

    /// <summary>读 <see cref="RowDoubleClickExpandsProperty"/>。</summary>
    public static bool GetRowDoubleClickExpands(TreeView tree) => tree.GetValue(RowDoubleClickExpandsProperty);

    /// <summary>写 <see cref="RowDoubleClickExpandsProperty"/>。</summary>
    public static void SetRowDoubleClickExpands(TreeView tree, bool value) => tree.SetValue(RowDoubleClickExpandsProperty, value);

    private static void OnRowDoubleClickExpandsChanged(TreeView tree, AvaloniaPropertyChangedEventArgs e)
    {
        // 先摘再挂：属性被改回 false、或同一棵树上重复赋值时，不留下重复订阅。
        // ⚠ 用 AddHandler(InputElement.DoubleTappedEvent) 而不是 `tree.DoubleTapped +=`：
        //   要的是**明确指名那个路由事件**——两种写法未必落在同一个 RoutedEvent 上，
        //   落错了不报错、只是永远收不到（本用例第一版就栽在这里，探针显示属性设上了却不翻）。
        //   handledEventsToo 保持默认 false：框架自己处理掉的那次（点在文字上）不该再来一遍。
        tree.RemoveHandler(InputElement.DoubleTappedEvent, OnDoubleTapped);
        if (e.GetNewValue<bool>()) tree.AddHandler(InputElement.DoubleTappedEvent, OnDoubleTapped);
    }

    private static void OnDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (ToggleRowFrom(e.Source)) e.Handled = true;
    }

    /// <summary>把这一下双击落在的那一行翻转展开态；返回是否真的翻了。</summary>
    private static bool ToggleRowFrom(object? source)
    {
        if (source is not Visual visual) return false;

        // 点在箭头上不归这里管：它自己就是 ToggleButton，两下已经各翻一次（净效果不变）——
        // 这里再翻一次反而把"双击箭头等于没点"改成了"双击箭头会展开"，那本来就不是坏的东西。
        if (visual.FindAncestorOfType<ToggleButton>(includeSelf: true) is not null) return false;

        // 叶子行没有子节点可展开：双击它在宿主那边通常另有语义（打开、启用…），不许抢。
        if (visual.FindAncestorOfType<TreeViewItem>(includeSelf: true) is not { ItemCount: > 0 } row) return false;

        row.IsExpanded = !row.IsExpanded;
        return true;
    }
}
