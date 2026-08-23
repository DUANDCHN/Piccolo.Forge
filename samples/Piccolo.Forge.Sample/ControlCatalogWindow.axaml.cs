// ⚠ 本文件里对 Avalonia 的**限定引用一律写 global::Avalonia.***。
// 原因：命名空间是 Piccolo.Forge.Sample，C# 由内向外找 —— 写 Avalonia.Thickness 会先撞上
// 兄弟命名空间 Piccolo.Forge.Avalonia（本工程引了它），解析成 Piccolo.Forge.Avalonia.Thickness 而报错。
// 顶部的 using 指令不受影响（它们全局解析），只有**限定名**会被就近劫持。
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia.Media;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Piccolo.Forge.Sample;

public partial class ControlCatalogWindow : Window
{
    public ControlCatalogWindow()
    {
        InitializeComponent();

        ProgressText.Text = $"接管进度 {Catalog.Adopted} / {Catalog.Total}";
        BuildNav();

        LightVariant.IsCheckedChanged += (_, _) => Apply();
        DarkVariant.IsCheckedChanged += (_, _) => Apply();

        var args = Environment.GetCommandLineArgs();
        if (args.Contains("--dark")) DarkVariant.IsChecked = true;
        Apply();

        // 默认落在第一个已接管的控件上，省得每次手点；--select <控件名> 可直接定位（截图用）。
        // 放到 Loaded 里：选中会触发 BringIntoView，而此刻树项还没测量，会把树滚到错误的位置。
        var wanted = args.SkipWhile(a => a != "--select").Skip(1).FirstOrDefault();
        Loaded += (_, _) => Select(wanted);

        // --sweep：把每个条目逐个选一遍再退出。隐式主题一变，很多控件内部的子控件会换模板，
        // 这一遍专门用来把"某个控件一渲染就崩"扫出来（日历就是这么抓到的）。
        if (args.Contains("--sweep"))
            Loaded += (_, _) =>
            {
                var names = Catalog.Groups.SelectMany(g => g.Entries)
                                          .Where(e => e.Adopted).Select(e => e.Name).ToList();
                var i = 0;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
                timer.Tick += (_, _) =>
                {
                    if (i >= names.Count)
                    {
                        timer.Stop();
                        Console.WriteLine($"SWEEP OK {names.Count}");
                        Environment.Exit(0);
                    }
                    Console.WriteLine($"SWEEP {names[i]}");
                    Select(names[i++]);
                };
                timer.Start();
            };

        // ⚠ 延迟不只是等布局 —— **Popup 打开后不跟随窗口移动**。截图脚本会先摆窗口再截，
        //   若下拉在移动之前就打开，它会留在旧位置，看起来像模板的 PlacementTarget 写错了
        //   （真因是 Popup 打开后不跟随窗口移动，而截图脚本先开下拉再摆窗口。）
        //   所以这里的延迟要**长于**截图脚本摆窗口的时间。
        if (args.Contains("--open"))
            Loaded += (_, _) =>
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3500) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    // 展开**自研那一列**的（右列，即最后一个）—— 展开态截图看不到，
                    // 模拟点击又会把别的窗口带到前台。两列同时展开会互相 light-dismiss，只能开一个。
                    var cb = Detail.GetLogicalDescendants().OfType<ComboBox>().LastOrDefault();
                    if (cb is not null) cb.IsDropDownOpen = true;
                    // 自动补全：填一段前缀再展开候选层（只开自研那一列的）
                    var acb = Detail.GetLogicalDescendants().OfType<AutoCompleteBox>().LastOrDefault();
                    if (acb is not null) { acb.Text = "MV"; acb.IsDropDownOpen = true; }

                    // 命令栏的溢出层同理，只开自研那一列的
                    var bar = Detail.GetLogicalDescendants().OfType<CommandBar>().LastOrDefault();
                    if (bar is not null) { bar.IsSticky = true; bar.IsOpen = true; }
                    var mi = Detail.GetLogicalDescendants().OfType<MenuItem>()
                                   .LastOrDefault(m => m.ItemCount > 0);
                    if (mi is not null) mi.IsSubMenuOpen = true;
                    // 选项卡全部切到**最后一页**：既能区分"模板绑定坏了"和"点击没命中"，
                    // 也能看到嵌在别的控件里的标签页（如取色面板的分量页）。
                    // ⚠ 用**可视树**：模板内部的元素（如取色面板里的 TabControl）不在逻辑树上，
                    //   GetLogicalDescendants 遍历不到它们。
                    foreach (var tc in Detail.GetVisualDescendants().OfType<TabControl>())
                        if (tc.ItemCount > 1) tc.SelectedIndex = tc.ItemCount - 1;
                    // 提示气泡：click at 不产生持续 hover，只能程序化打开。
                    var tipHost = Detail.GetLogicalDescendants().OfType<Border>()
                                        .LastOrDefault(b => ToolTip.GetTip(b) is not null);
                    if (tipHost is not null) ToolTip.SetIsOpen(tipHost, true);
                };
                timer.Start();
            };
    }

    void Apply()
        => RequestedThemeVariant = DarkVariant.IsChecked == true ? ThemeVariant.Dark : ThemeVariant.Light;

    void BuildNav()
    {
        foreach (var group in Catalog.Groups)
        {
            var node = new TreeViewItem
            {
                Header = $"{group.Name}  ({group.Entries.Count(e => e.Adopted)}/{group.Entries.Length})",
                IsExpanded = group.Entries.Any(e => e.Adopted),
            };
            foreach (var entry in group.Entries)
            {
                var text = new TextBlock
                {
                    Text = entry.Cn.Length > 0 ? $"{entry.Name}   {entry.Cn}" : entry.Name,
                };
                if (!entry.Adopted) text.Classes.Add("pending");
                node.Items.Add(new TreeViewItem { Header = text, Tag = entry });
            }
            Nav.Items.Add(node);
        }

        Nav.SelectionChanged += (_, _) =>
        {
            if (Nav.SelectedItem is TreeViewItem { Tag: Entry e }) Show(e);
        };
    }

    /// <summary>选中指定控件；没指定或找不到就落在第一个已接管的上。</summary>
    void Select(string? name)
    {
        var all = Nav.Items.OfType<TreeViewItem>()
            .SelectMany(g => g.Items.OfType<TreeViewItem>().Select(i => (group: g, item: i)))
            .ToList();
        var hit = name is null
            ? default
            : all.FirstOrDefault(x => x.item.Tag is Entry e && e.Name == name);
        if (hit.item is null)
            hit = all.FirstOrDefault(x => x.item.Tag is Entry { Adopted: true });

        // ⚠ 元组用 default 表示"没找到"时，group 与 item **一起**是 null，
        //   但只检查其中一个**不会**让另一个也收窄 —— 于是 hit.group 仍是"可能为空"。
        //   用位置模式整对一起判，两个同时收窄，也把"它们成对出现"这件事写进代码。
        if (hit is not ({ } group, { } item)) return;
        group.IsExpanded = true;
        item.IsSelected = true;
    }

    /// <summary>渲染一个控件的演示页：每行一个用途。</summary>
    void Show(Entry entry)
    {
        Detail.Children.Clear();

        Detail.Children.Add(new TextBlock
        {
            Text = entry.Cn.Length > 0 ? $"{entry.Name} — {entry.Cn}" : entry.Name,
            Classes = { "h" },
            Margin = new global::Avalonia.Thickness(0, 0, 0, 16),
        });

        if (entry.Rows.Length == 0)
        {
            Detail.Children.Add(new TextBlock
            {
                Text = entry.Adopted
                    ? $"已接管，但它只作为 {entry.SeeAlso} 的一部分出现，没有独立对照页 —— 去那一页看。"
                    : "尚未接管：本主题没有给这个控件模板。\n"
                      + "接管后在 Catalog.cs 里给这个条目补 Row 即可。",
                Classes = { "muted" },
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("120,*"),
            RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", entry.Rows.Length))),
        };

        for (var i = 0; i < entry.Rows.Length; i++)
        {
            var row = entry.Rows[i];
            AddCell(grid, i, 0, new TextBlock { Text = row.Label, Classes = { "label" }, Margin = new global::Avalonia.Thickness(0, 12, 12, 0) });

            var sample = row.Build();
            sample.HorizontalAlignment = HorizontalAlignment.Left;
            sample.Margin = new global::Avalonia.Thickness(0, 8, 0, 16);
            AddCell(grid, i, 1, sample);
        }

        Detail.Children.Add(grid);
    }

    static void AddCell(Grid grid, int row, int col, Control c)
    {
        Grid.SetRow(c, row);
        Grid.SetColumn(c, col);
        c.Margin = new global::Avalonia.Thickness(0, 0, 12, 0);
        grid.Children.Add(c);
    }



    /// <summary>递归收集所有资源 **Key**。⚠ 直接 foreach 一个 ResourceDictionary
    /// **不含**它 MergedDictionaries 里的内容 —— 而自研主题恰恰全在合并字典里，
    /// 只遍历顶层会一条都拿不到（而且不报错）。</summary>
    static IEnumerable<object> AllKeys(IResourceDictionary dict)
    {
        foreach (var kv in dict) yield return kv.Key;
        foreach (var merged in dict.MergedDictionaries)
        {
            var rd = merged as IResourceDictionary ?? (merged as ResourceInclude)?.Loaded;
            if (rd is not null)
                foreach (var key in AllKeys(rd))
                    yield return key;
        }
    }
}
