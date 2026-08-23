// ⚠ 本文件里对 Avalonia 的**限定引用一律写 global::Avalonia.***。
// 原因：命名空间是 Piccolo.Forge.Sample，C# 由内向外找 —— 写 Avalonia.Thickness 会先撞上
// 兄弟命名空间 Piccolo.Forge.Avalonia（本工程引了它），解析成 Piccolo.Forge.Avalonia.Thickness 而报错。
// 顶部的 using 指令不受影响（它们全局解析），只有**限定名**会被就近劫持。
using Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Piccolo.Forge.Sample;

/// <summary>演示页里的一行：左边写用途，右边是照这个用途构造出来的控件。</summary>
/// <remarks>
/// 原先这里是两列（左 Semi 原貌、右自研），用于开发期的同屏 A/B。搬进本仓时去掉了 Semi 那一列
/// —— 主题库不该依赖另一个主题库。树与条目**原样保留**：它同时是覆盖清单与人工验收单。
/// </remarks>
public record Row(string Label, Func<Control> Build);

/// <summary>一个控件的对照页。</summary>
public record Entry(string Name, string Cn = "", params Row[] Rows)
{
    /// <summary>已接管但没有独立对照页的（如 ButtonSpinner、ComboBoxItem 这类只作为
    /// 别的控件的一部分出现的）显式标一下，否则会被算成未接管。</summary>
    public bool Inner { get; init; }

    /// <summary>Inner 条目指向承载它的控件，点进来时告诉人去哪看。</summary>
    public string? SeeAlso { get; init; }

    public bool Adopted => Inner || Rows.Length > 0;
}

public record Group(string Name, params Entry[] Entries);

/// <summary>
/// 控件清单 —— 树的完整性就是覆盖进度表，别删条目。已接管的填 Rows，未接管的留空。
///
/// Avalonia 的具体（非抽象）TemplatedControl 共 100 个，这里列 89 个。
/// **有意不列的 11 个**是基类与宿主壳，它们没有自身外观、不需要控件主题：
///   ContentControl / HeaderedContentControl / HeaderedItemsControl /
///   HeaderedSelectingItemsControl / SelectingItemsControl / TemplatedControl /
///   UserControl / WindowBase / EmbeddableControlRoot / OverlayPopupHost / PopupRoot
/// 写在这里是为了让"89 不是漏了 11 个"这件事可查 —— 数字对不上时先看这段。
/// </summary>
public static class Catalog
{
    public static readonly Group[] Groups =
    [
        new("按钮",
            new Entry("Button", "按钮",
                new Row("常规",
                    () => new Button { Content = "确定" }),
                new Row("主按钮",
                    () => Classed(new Button { Content = "应用" }, "accent")),
                new Row("禁用",
                    () => new Button { Content = "不可用", IsEnabled = false }),
                new Row("禁用·主按钮",
                    () => Classed(new Button { Content = "应用", IsEnabled = false }, "accent")),
                new Row("对话框按钮组",
                    () => Horiz(new Button { Content = "取消" },
                                Classed(new Button { Content = "确定" }, "accent"))),
                new Row("长短文字",
                    () => Horiz(new Button { Content = "是" }, new Button { Content = "保存并关闭全部窗口" }))),
            new Entry("RepeatButton", "长按连发",
                new Row("常规",
                                () => new RepeatButton { Content = "按住加速" })),
            new Entry("ToggleButton", "开关按钮",
                new Row("未按 / 按下",
                    () => Horiz(new ToggleButton { Content = "网格" },
                                new ToggleButton { Content = "刻度尺", IsChecked = true }))),
            new Entry("SplitButton", "拆分按钮",
                new Row("常规",
                                () => new SplitButton { Content = "保存" })),
            new Entry("ToggleSplitButton", "拆分开关",
                new Row("未按 / 按下（选中态待补，见 Misc.axaml）",
                    () => Horiz(new ToggleSplitButton { Content = "网格" },
                                new ToggleSplitButton { Content = "刻度尺", IsChecked = true }))),
            new Entry("DropDownButton", "下拉按钮",
                new Row("常规",
                                () => new DropDownButton { Content = "导出为" })),
            new Entry("HyperlinkButton", "链接按钮",
                new Row("常规 / 禁用",
                    () => Horiz(new HyperlinkButton { Content = "查看使用指南" },
                                new HyperlinkButton { Content = "不可用", IsEnabled = false }))),
            new Entry("CommandBarButton", "命令栏按钮",
                new Row("常规 / 禁用",
                    () => Horiz(Cbb("保存"), Dis(Cbb("撤销"))))),
            new Entry("CommandBarToggleButton", "命令栏开关",
                new Row("未选 / 选中",
                    () => Horiz(Cbt("网格", false), Cbt("标尺", true))))),

        new("输入",
            new Entry("TextBox", "文本框",
                new Row("常规",
                    () => new TextBox { Width = 180, Text = "192.168.1.100" }),
                new Row("占位符",
                    () => new TextBox { Width = 180, PlaceholderText = "请输入相机 IP" }),
                new Row("禁用",
                    () => new TextBox { Width = 180, Text = "只读", IsEnabled = false }),
                new Row("多行",
                    () => new TextBox { Width = 180, Height = 64, AcceptsReturn = true, Text = "第一行\n第二行" })),
            new Entry("NumericUpDown", "数字框",
                new Row("常规",
                    () => new NumericUpDown { Width = 140, Value = 1000, Minimum = 0, Maximum = 100000, FormatString = "0" }),
                new Row("禁用",
                    () => new NumericUpDown { Width = 140, Value = 1000, FormatString = "0", IsEnabled = false }),
                new Row("箭头在左",
                    () => new NumericUpDown { Width = 140, Value = 50, FormatString = "0",
                                              ButtonSpinnerLocation = Location.Left })),
            new Entry("ButtonSpinner", "步进器") { Inner = true, SeeAlso = "NumericUpDown" },
            new Entry("MaskedTextBox", "掩码输入",
                new Row("IP 掩码",
                    () => new MaskedTextBox { Width = 180, Mask = "000.000.000.000" })),
            new Entry("AutoCompleteBox", "自动补全",
                new Row("输入 MV 过滤", () => Acb()))),

        new("选择",
            new Entry("ComboBox", "下拉框",
                new Row("常规",
                    () => Items(new ComboBox { Width = 180, SelectedIndex = 0 })),
                new Row("占位符",
                    () => new ComboBox { Width = 180, PlaceholderText = "请选择格式" }),
                new Row("禁用",
                    () => Items(new ComboBox { Width = 180, SelectedIndex = 0, IsEnabled = false }))),
            new Entry("ComboBoxItem", "下拉项") { Inner = true, SeeAlso = "ComboBox" },
            new Entry("CheckBox", "复选框",
                new Row("未选 / 已选 / 三态",
                    () => Horiz(new CheckBox { Content = "未选" },
                                new CheckBox { Content = "已选", IsChecked = true },
                                new CheckBox { Content = "三态", IsThreeState = true, IsChecked = null })),
                new Row("禁用",
                    () => Horiz(new CheckBox { Content = "未选", IsEnabled = false },
                                new CheckBox { Content = "已选", IsChecked = true, IsEnabled = false })),
                new Row("成组竖排",
                    () => Vert(new CheckBox { Content = "自动曝光" },
                               new CheckBox { Content = "自动增益", IsChecked = true },
                               new CheckBox { Content = "自动白平衡" }))),
            new Entry("RadioButton", "单选钮",
                new Row("未选 / 已选",
                    () => Horiz(new RadioButton { Content = "未选", GroupName = "b" },
                                new RadioButton { Content = "已选", GroupName = "b", IsChecked = true })),
                new Row("禁用",
                    () => Horiz(new RadioButton { Content = "未选", IsEnabled = false },
                                new RadioButton { Content = "已选", IsChecked = true, IsEnabled = false })),
                new Row("成组竖排",
                    () => Vert(new RadioButton { Content = "连续采集", GroupName = "d", IsChecked = true },
                               new RadioButton { Content = "软触发", GroupName = "d" },
                               new RadioButton { Content = "硬触发", GroupName = "d" }))),
            new Entry("ToggleSwitch", "开关",
                new Row("关 / 开 / 禁用",
                    () => Vert(new ToggleSwitch { Content = "自动重连" },
                               new ToggleSwitch { Content = "巨型帧", IsChecked = true },
                               new ToggleSwitch { Content = "不可用", IsEnabled = false }))),
            new Entry("Slider", "滑块",
                new Row("常规",
                    () => new Slider { Width = 220, Minimum = 0, Maximum = 100, Value = 40 }),
                new Row("禁用",
                    () => new Slider { Width = 220, Value = 40, IsEnabled = false }))),

        new("列表与表格",
            new Entry("ListBox", "列表",
                new Row("选中 / 失焦选中", () => Lb()),
                new Row("禁用", () => Lb(enabled: false))),
            new Entry("ListBoxItem", "列表项") { Inner = true, SeeAlso = "ListBox" },
            new Entry("TreeView", "树",
                new Row("多层 / 展开收起", () => Tv())),
            new Entry("TreeViewItem", "树节点") { Inner = true, SeeAlso = "TreeView" },
            new Entry("ItemsControl", "条目容器") { Inner = true, SeeAlso = "ListBox" },
            new Entry("DataGrid", "数据表",
                new Row("三列 / 可排序 / 选中", () => Dg())),
            new Entry("DataGridCell", "数据表·单元") { Inner = true, SeeAlso = "DataGrid" },
            new Entry("DataGridColumnHeader", "数据表·表头") { Inner = true, SeeAlso = "DataGrid" },
            new Entry("DataGridRow", "数据表·行") { Inner = true, SeeAlso = "DataGrid" },
            new Entry("DataGridRowHeader", "数据表·行头") { Inner = true, SeeAlso = "DataGrid" },
            new Entry("DataGridRowGroupHeader", "数据表·分组头") { Inner = true, SeeAlso = "DataGrid" },
            new Entry("TableView", "表格") { Inner = true, SeeAlso = "DataGrid" },
            new Entry("TableViewCell", "表格·单元") { Inner = true, SeeAlso = "DataGrid" },
            new Entry("TableViewColumnHeader", "表格·表头") { Inner = true, SeeAlso = "DataGrid" },
            new Entry("TableViewRow", "表格·行") { Inner = true, SeeAlso = "DataGrid" },
            new Entry("Carousel", "轮播") { Inner = true, SeeAlso = "PipsPager" },
            new Entry("CarouselPage", "轮播页") { Inner = true, SeeAlso = "PipsPager" },
            new Entry("PipsPager", "点状分页",
                new Row("5 页",
                                () => new PipsPager { NumberOfPages = 5, SelectedPageIndex = 1 }))),

        new("容器与布局",
            new Entry("Expander", "折叠面板",
                new Row("收起 / 展开",
                    () => Vert(Exp("采集参数", false), Exp("图像参数", true))),
                new Row("禁用",
                    () => Exp("不可用的分组", false, enabled: false))),
            new Entry("GroupBox", "分组框",
                new Row("常规",
                    () => new GroupBox { Header = "网络", Width = 260,
                                         Content = Vert(new CheckBox { Content = "巨型帧" },
                                                        new CheckBox { Content = "自动协商", IsChecked = true }) })),
            new Entry("TabControl", "选项卡",
                new Row("三页", () => Tabs())),
            new Entry("TabItem", "选项卡项") { Inner = true, SeeAlso = "TabControl" },
            new Entry("TabStrip", "选项卡条",
                new Row("纯标签条", () => Strip())),
            new Entry("TabStripItem", "选项卡条项") { Inner = true, SeeAlso = "TabStrip" },
            new Entry("SplitView", "抽屉布局",
                new Row("面板 + 内容", () => Sv())),
            new Entry("ScrollViewer", "滚动容器",
                new Row("竖向滚动",
                    () => Scrollable(new ScrollViewer { Width = 220, Height = 110 })),
                new Row("双向滚动",
                    () => Scrollable(new ScrollViewer { Width = 220, Height = 110,
                        HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }, wide: true))),
            new Entry("ScrollBar", "滚动条") { Inner = true, SeeAlso = "ScrollViewer" },
            new Entry("GridSplitter", "分隔条",
                new Row("竖向分隔", () => Splitter())),
            new Entry("TransitioningContentControl", "过渡容器") { Inner = true, SeeAlso = "ListBox" },
            new Entry("RefreshContainer", "下拉刷新",
                new Row("向下拖动列表", () => Rc())),
            new Entry("RefreshVisualizer", "刷新指示") { Inner = true, SeeAlso = "RefreshContainer" }),

        new("菜单与弹出",
            new Entry("Menu", "菜单栏",
                new Row("菜单栏", () => MenuBar())),
            new Entry("MenuItem", "菜单项",
                new Row("普通项", () => MenuPanel()),
                new Row("勾选 / 分隔 / 子菜单", () => MenuPanel(rich: true))),
            new Entry("ContextMenu", "右键菜单",
                new Row("在方块上右键", () => RightClickTarget())),
            new Entry("MenuFlyoutPresenter", "菜单浮出") { Inner = true, SeeAlso = "MenuItem" },
            new Entry("Separator", "分隔线") { Inner = true, SeeAlso = "MenuItem" },
            new Entry("FlyoutPresenter", "浮出层") { Inner = true, SeeAlso = "Button" },
            new Entry("ToolTip", "提示气泡",
                new Row("悬停查看", () => Tip())),
            new Entry("NativeMenuBar", "系统菜单") { Inner = true, SeeAlso = "Menu" }),

        new("日期与时间",
            new Entry("Calendar", "日历",
                new Row("月视图",
                                  () => new Calendar { SelectedDate = System.DateTime.Today })),
            new Entry("CalendarButton", "日历·月钮") { Inner = true, SeeAlso = "Calendar" },
            new Entry("CalendarDayButton", "日历·日钮") { Inner = true, SeeAlso = "Calendar" },
            new Entry("CalendarItem", "日历·主体") { Inner = true, SeeAlso = "Calendar" },
            new Entry("CalendarDatePicker", "日历选择器",
                new Row("输入框 + 日历弹出",
                    () => new CalendarDatePicker { Width = 200, SelectedDate = System.DateTime.Today })),
            new Entry("DatePicker", "日期选择",
                new Row("年 / 月 / 日 三段",
                    () => new DatePicker { SelectedDate = System.DateTimeOffset.Now })),
            new Entry("DatePickerPresenter", "日期选择·面板") { Inner = true, SeeAlso = "DatePicker" },
            new Entry("TimePicker", "时间选择",
                new Row("时 / 分 / 秒",
                    () => new TimePicker { SelectedTime = System.TimeSpan.FromHours(14.5), UseSeconds = true })),
            new Entry("TimePickerPresenter", "时间选择·面板") { Inner = true, SeeAlso = "TimePicker" }),

        new("颜色",
            new Entry("ColorPicker", "取色器",
                new Row("按钮 + 弹出面板",
                    () => new ColorPicker { Color = Colors.DodgerBlue })),
            new Entry("ColorView", "取色面板",
                new Row("完整面板",
                    () => new ColorView { Color = Colors.OrangeRed, Width = 240 })),
            new Entry("ColorPreviewer", "颜色预览") { Inner = true, SeeAlso = "ColorView" },
            new Entry("ColorSlider", "颜色滑条") { Inner = true, SeeAlso = "ColorView" },
            new Entry("ColorSpectrum", "色谱") { Inner = true, SeeAlso = "ColorView" }),

        new("反馈与提示",
            new Entry("ProgressBar", "进度条",
                new Row("进行 / 完成 / 失败",
                    () => Vert(new ProgressBar { Width = 220, Value = 45 },
                               Classed(new ProgressBar { Width = 220, Value = 100 }, "passed"),
                               Classed(new ProgressBar { Width = 220, Value = 70 }, "failed")))),
            new Entry("NotificationCard", "通知卡",
                new Row("四档严重度",
                    () => Vert(Nc(NotificationType.Information), Nc(NotificationType.Success),
                               Nc(NotificationType.Warning), Nc(NotificationType.Error)))),
            // ⚠ 通知管理器**无法同屏对照**：它把通知放进窗口的全局浮层（OverlayLayer），
            //   不在任何一列的子树里，拿不到那一列挂的主题 —— 两列弹出的都是基础主题的卡片。
            //   静态样式看 NotificationCard 那页；入场动画只能在真实宿主里看。
            new Entry("WindowNotificationManager", "通知管理器",
                new Row("点按钮弹（看入场动画与关闭）", () => NcLive(true))),
            new Entry("DataValidationErrors", "校验错误",
                new Row("带错误的输入框", () => Dve()))),

        new("图标与文本",
            new Entry("PathIcon", "路径图标",
                new Row("图标 + 文字",
                    () => Horiz(Icon(), new TextBlock { Text = "对齐基线", VerticalAlignment = VerticalAlignment.Center }))),
            new Entry("Label", "标签",
                new Row("常规 / 错误 / 警告",
                    () => Vert(new Label { Content = "常规标签" },
                               Classed(new Label { Content = "错误提示" }, "error"),
                               Classed(new Label { Content = "警告提示" }, "warning"))))),

        new("页面导航",
            new Entry("CommandBar", "命令栏",
                new Row("主命令 + 溢出", () => Cmd())),
            new Entry("CommandBarSeparator", "命令栏分隔") { Inner = true, SeeAlso = "CommandBar" },
            new Entry("ContentPage", "内容页",
                new Row("标题栏 + 内容", () => PageBox(ContentPg()))),
            new Entry("NavigationPage", "导航页",
                new Row("栈顶 + 返回", () => PageBox(NavPg()))),
            new Entry("TabbedPage", "标签页",
                new Row("三页签", () => PageBox(TabPg()))),
            new Entry("DrawerPage", "抽屉页",
                new Row("常驻分栏",
                    () => PageBox(DrawerPg(DrawerBehavior.Locked))),
                new Row("浮出（内容上有遮罩）",
                    () => PageBox(DrawerPg(DrawerBehavior.Flyout)))),
            new Entry("PageNavigationHost", "导航宿主") { Inner = true, SeeAlso = "NavigationPage" }),

        new("窗口与其他",
            new Entry("Window", "窗口") { Inner = true, SeeAlso = "ToolTip" },
            new Entry("Thumb", "拖拽块") { Inner = true, SeeAlso = "Slider" },
            new Entry("TextSelectionHandle", "选区手柄") { Inner = true, SeeAlso = "TextBox" }),
    ];

    public static int Total => Groups.Sum(g => g.Entries.Length);
    public static int Adopted => Groups.Sum(g => g.Entries.Count(e => e.Adopted));

    // —— 构造小工具 ——

    /// <summary>挂一个具名 ControlTheme（及可选 Classes）。</summary>
    static Control Themed(Control c, string theme, params string[] classes)
    {
        if (global::Avalonia.Application.Current!.TryFindResource(theme, out var t) && t is ControlTheme ct)
            c.Theme = ct;
        foreach (var cls in classes) c.Classes.Add(cls);
        return c;
    }

    static Control Classed(Control c, params string[] classes)
    {
        foreach (var cls in classes) c.Classes.Add(cls);
        return c;
    }

    /// <summary>一个"下载"图标，纯路径，不依赖图标包。</summary>
    static Control Icon() => new PathIcon
    {
        Data = global::Avalonia.Media.Geometry.Parse(
            "M8 1 L8 10 M4.5 6.5 L8 10 L11.5 6.5 M2 13 L14 13"),
        Width = 16, Height = 16,
        VerticalAlignment = VerticalAlignment.Center,
    };

    static Control Sv()
    {
        var sv = new SplitView
        {
            Width = 300, Height = 110,
            IsPaneOpen = true,
            OpenPaneLength = 110,
            DisplayMode = SplitViewDisplayMode.Inline,
            Pane = new TextBlock { Text = "侧栏", Margin = new global::Avalonia.Thickness(10) },
            Content = new TextBlock { Text = "内容区", Margin = new global::Avalonia.Thickness(10) },
        };
        return sv;
    }

    /// <summary>⚠ 严重度要设 NotificationType **属性**，不能 Classes.Add ——
    /// 控件在 UpdateNotificationType 里打的是**伪类**（:success 等），
    /// 加同名 class 不会命中 `^:success` 选择器，四张卡片会全是默认档。</summary>
    static Control Nc(NotificationType type) => new NotificationCard
    {
        NotificationType = type,
        Content = type switch
        {
            NotificationType.Success => "相机已连接",
            NotificationType.Warning => "帧率低于设定值",
            NotificationType.Error   => "取流失败：链路中断",
            _                        => "已保存快照",
        },
    };

    /// <summary>真实路径演示：走 WindowNotificationManager 弹出，才能看到入场动画 ——
    /// 直接 new 一张卡摆上去是没有过渡触发点的（属性从一开始就是终值）。</summary>
    static Control NcLive(bool forge)
    {
        var host = new Panel { Width = 360, Height = 150 };
        var mgr = new WindowNotificationManager
        {
            Position = NotificationPosition.BottomRight,
            MaxItems = 4,
        };
        // ⚠ 管理器内部是自己 new NotificationCard 的，既不经过 ApplyForgeTheme，
        //   也拿不到外层子树注册的隐式主题。要让自研主题生效，只能挂在**管理器自己**的
        //   资源里 —— 卡片是它的后代，查得到。
        if (forge && global::Avalonia.Application.Current!.TryFindResource("Forge.NotificationCard", out var t)
            && t is ControlTheme ct)
        {
            mgr.Resources[typeof(NotificationCard)] = ct;
        }
        host.Children.Add(mgr);
        var btn = new Button { Content = "弹一条通知", HorizontalAlignment = HorizontalAlignment.Left,
                               VerticalAlignment = VerticalAlignment.Top };
        var i = 0;
        btn.Click += (_, _) =>
        {
            var kinds = new[]
            {
                (NotificationType.Information, "已保存快照"),
                (NotificationType.Success, "相机已连接"),
                (NotificationType.Warning, "帧率低于设定值"),
                (NotificationType.Error, "取流失败：链路中断"),
            };
            var (type, text) = kinds[i++ % kinds.Length];
            mgr.Show(new Notification("", text, type));
        };
        host.Children.Add(btn);
        return host;
    }

    static Control Dve()
    {
        var tb = new TextBox { Width = 200, Text = "999.999.999.999" };
        var d = new DataValidationErrors { Content = tb, Width = 200 };
        DataValidationErrors.SetError(d, new System.Exception("不是合法的 IPv4 地址"));
        return d;
    }

    static Control Tip()
    {
        var b = new Border
        {
            Width = 180, Height = 44,
            BorderThickness = new global::Avalonia.Thickness(1),
            BorderBrush = global::Avalonia.Media.Brushes.Gray,
            Child = new TextBlock { Text = "把鼠标停在这里", HorizontalAlignment = HorizontalAlignment.Center,
                                    VerticalAlignment = VerticalAlignment.Center },
        };
        ToolTip.SetTip(b, "这是一条提示：亮色档下它也是深底浅字");
        return b;
    }

    /// <summary>一张三列的相机表，够看清表头、网格线、行选中。</summary>
    static Control Dg()
    {
        var dg = new DataGrid
        {
            Width = 360, Height = 150,
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserSortColumns = true,
            ItemsSource = new[]
            {
                new CamRow("GigE 相机 01", "192.168.1.11", "在线"),
                new CamRow("GigE 相机 02", "192.168.1.12", "离线"),
                new CamRow("USB3 相机 01", "—", "在线"),
                new CamRow("虚拟相机", "—", "在线"),
            },
        };
        dg.Columns.Add(new DataGridTextColumn { Header = "名称", Binding = new global::Avalonia.Data.Binding(nameof(CamRow.Name)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        dg.Columns.Add(new DataGridTextColumn { Header = "IP", Binding = new global::Avalonia.Data.Binding(nameof(CamRow.Ip)) });
        dg.Columns.Add(new DataGridTextColumn { Header = "状态", Binding = new global::Avalonia.Data.Binding(nameof(CamRow.State)) });
        return dg;
    }

    public record CamRow(string Name, string Ip, string State);

    static Control Tabs()
    {
        var tc = new TabControl { Width = 320, Height = 140 };
        foreach (var (h, c) in new[] { ("常用", "常用属性页"), ("高级", "高级属性页"), ("关于", "关于页") })
            tc.Items.Add(new TabItem { Header = h, Content = new TextBlock { Text = c, Margin = new global::Avalonia.Thickness(12) } });
        return tc;
    }

    static Control Strip()
    {
        var ts = new TabStrip { SelectedIndex = 0 };
        foreach (var h in new[] { "并排", "擦除", "差分", "闪烁" })
            ts.Items.Add(new TabStripItem { Content = h });
        return ts;
    }

    static Control Acb() => new AutoCompleteBox
    {
        Width = 200,
        PlaceholderText = "输入相机型号",
        FilterMode = AutoCompleteFilterMode.Contains,
        ItemsSource = new[] { "MV-CA013-A0GM", "MV-CA020-20GC", "MV-CA050-10GM", "acA1300-30gm", "acA2500-14gm" },
    };

    // ⚠ 指示器截图截不到：RefreshVisualizerState 是只读 direct 属性装不进去，
    //   RequestRefresh() 也只是发事件、不把指示器挪进可视区 —— 只有真拖一把才看得见。
    //   这不是模板的问题（真因是 Popup 打开后不跟随窗口移动）。

    static Control Rc()
    {
        var list = new StackPanel();
        for (var i = 1; i <= 8; i++) list.Children.Add(new TextBlock { Text = $"记录 {i}", Margin = new Thickness(8, 4) });
        var rc = new RefreshContainer
        {
            Width = 200, Height = 120,
            IsMouseEnabled = true,
            Content = new ScrollViewer { Content = list },
        };
        // 取了 deferral 就一直不还 —— 让"刷新中"停住，截图才看得到指示器
        rc.RefreshRequested += (_, e) => e.GetDeferral();
        return rc;
    }

    // 命令栏按钮走 Label + Icon，不是 Content
    static CommandBarButton Cbb(string label) => new() { Label = label, Icon = Glyph() };
    static CommandBarToggleButton Cbt(string label, bool on) => new() { Label = label, Icon = Glyph(), IsChecked = on };
    static T Dis<T>(T c) where T : Control { c.IsEnabled = false; return c; }

    // 占位图标：描边跟着按钮前景走，选中态翻白才不至于糊在主色底上
    static Control Glyph()
    {
        var r = new global::Avalonia.Controls.Shapes.Rectangle
        {
            Width = 12, Height = 12, RadiusX = 2, RadiusY = 2, StrokeThickness = 1.2,
        };
        r.Bind(global::Avalonia.Controls.Shapes.Shape.StrokeProperty, new global::Avalonia.Data.Binding("Foreground")
        {
            RelativeSource = new global::Avalonia.Data.RelativeSource(global::Avalonia.Data.RelativeSourceMode.FindAncestor)
            {
                AncestorType = typeof(TemplatedControl),
            },
        });
        return r;
    }

    static Control Cmd()
    {
        var bar = new CommandBar { Width = 340 };
        bar.PrimaryCommands.Add(Cbb("运行"));
        bar.PrimaryCommands.Add(Cbt("连续", true));
        bar.PrimaryCommands.Add(new CommandBarSeparator());
        bar.PrimaryCommands.Add(Cbb("快照"));
        bar.SecondaryCommands.Add(Cbb("导出图像"));
        bar.SecondaryCommands.Add(new CommandBarSeparator());
        bar.SecondaryCommands.Add(Cbb("相机设置"));
        return bar;
    }

    // 页面族本身要占满宿主，装进定框才看得出栏与内容的分界
    static Control PageBox(Control page) => new Border
    {
        Width = 300, Height = 180,
        BorderThickness = new Thickness(1),
        BorderBrush = Brushes.Silver,
        Child = page,
    };

    static ContentPage ContentPg() => new()
    {
        Header = "相机参数",
        Content = new TextBlock { Text = "曝光 8000 µs\n增益 6.0 dB", Margin = new Thickness(12) },
    };

    // ⚠ NavigationPage.Pages 不能直接赋值（框架会抛），只能 PushAsync 一页一页压
    static Control NavPg()
    {
        var nav = new NavigationPage();
        _ = nav.PushAsync(new ContentPage
        {
            Header = "设备列表",
            Content = new TextBlock { Text = "根页", Margin = new Thickness(12) },
        });
        _ = nav.PushAsync(new ContentPage
        {
            Header = "相机参数",
            Content = new TextBlock { Text = "栈顶页（左上有返回）", Margin = new Thickness(12) },
        });
        return nav;
    }

    static Control TabPg() => new TabbedPage
    {
        Pages = new[]
        {
            new ContentPage { Header = "取像", Content = new TextBlock { Text = "取像", Margin = new Thickness(12) } },
            new ContentPage { Header = "标定", Content = new TextBlock { Text = "标定", Margin = new Thickness(12) } },
            new ContentPage { Header = "结果", Content = new TextBlock { Text = "结果", Margin = new Thickness(12) } },
        },
    };

    // behavior=Locked 才是常驻分栏；默认 Auto 在窄页面上会转成浮出，内容上会盖一层遮罩
    static Control DrawerPg(DrawerBehavior behavior) => new DrawerPage
    {
        IsOpen = true,
        DrawerLength = 110,
        DisplayMode = SplitViewDisplayMode.Inline,
        DrawerBehavior = behavior,
        DrawerHeader = "工程",
        Drawer = new StackPanel
        {
            Margin = new Thickness(8),
            Children = { new TextBlock { Text = "相机" }, new TextBlock { Text = "光源" }, new TextBlock { Text = "算法" } },
        },
        Content = new TextBlock { Text = "抽屉页内容", Margin = new Thickness(12) },
    };

    static Control Splitter()
    {
        var g = new Grid { Width = 260, Height = 90, ColumnDefinitions = new ColumnDefinitions("*,Auto,*") };
        var left = new Border { Background = global::Avalonia.Media.Brushes.Transparent,
                                Child = new TextBlock { Text = "左", HorizontalAlignment = HorizontalAlignment.Center,
                                                        VerticalAlignment = VerticalAlignment.Center } };
        var right = new Border { Child = new TextBlock { Text = "右", HorizontalAlignment = HorizontalAlignment.Center,
                                                        VerticalAlignment = VerticalAlignment.Center } };
        var sp = new GridSplitter { Width = 1 };
        Grid.SetColumn(left, 0); Grid.SetColumn(sp, 1); Grid.SetColumn(right, 2);
        g.Children.Add(left); g.Children.Add(sp); g.Children.Add(right);
        return g;
    }

    static Control Lb(bool enabled = true)
    {
        var lb = new ListBox { Width = 220, Height = 120, SelectedIndex = 1, IsEnabled = enabled };
        foreach (var s in new[] { "GigE 相机 01", "GigE 相机 02", "USB3 相机 01", "虚拟相机" })
            lb.Items.Add(new ListBoxItem { Content = s });
        return lb;
    }

    static Control Tv()
    {
        var tv = new TreeView { Width = 260, Height = 190 };
        foreach (var (root, kids) in new[]
                 {
                     ("本机网卡", new[] { "以太网 (192.168.1.10)", "无线网卡" }),
                     ("采集参数", new[] { "曝光", "增益", "帧率" }),
                 })
        {
            var node = new TreeViewItem { Header = root, IsExpanded = true };
            foreach (var k in kids) node.Items.Add(new TreeViewItem { Header = k });
            tv.Items.Add(node);
        }
        tv.Items.Add(new TreeViewItem { Header = "没有子节点的项" });
        return tv;
    }

    static Control Exp(string header, bool expanded, bool enabled = true) => new Expander
    {
        Header = header,
        IsExpanded = expanded,
        IsEnabled = enabled,
        Width = 260,
        Content = Vert(new CheckBox { Content = "自动曝光", IsChecked = true },
                       new CheckBox { Content = "自动增益" }),
    };

    static Control Vert(params Control[] items)
    {
        var sp = new StackPanel { Spacing = 4 };
        foreach (var i in items) sp.Children.Add(i);
        return sp;
    }

    static Control Horiz(params Control[] items)
    {
        var sp = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
        foreach (var i in items) sp.Children.Add(i);
        return sp;
    }

    static Control MenuBar()
    {
        var m = new Menu();
        foreach (var (head, items) in new[]
                 {
                     ("文件(_F)", new[] { "打开图像…", "保存快照", "退出" }),
                     ("视图(_V)", new[] { "相机列表", "图像算法", "日志" }),
                     ("帮助(_H)", new[] { "使用指南", "关于" }),
                 })
        {
            var top = new MenuItem { Header = head };
            foreach (var it in items) top.Items.Add(new MenuItem { Header = it });
            m.Items.Add(top);
        }
        return m;
    }

    /// <summary>菜单面板本身没法脱离弹出层单独摆，用一个常驻的 ContextMenu 宿主替代不现实，
    /// 所以这里直接摆一列 MenuItem —— 看的是项的排版（图标列对齐、快捷键、勾选、分隔线），
    /// 弹出层的外观在「菜单栏」那行看。</summary>
    static Control MenuPanel(bool rich = false)
    {
        var host = new StackPanel { Width = 240 };
        if (!rich)
        {
            host.Children.Add(new MenuItem { Header = "打开图像…", InputGesture = new KeyGesture(Key.O, KeyModifiers.Control) });
            host.Children.Add(new MenuItem { Header = "保存快照", InputGesture = new KeyGesture(Key.S, KeyModifiers.Control) });
            host.Children.Add(new MenuItem { Header = "不可用的项", IsEnabled = false });
        }
        else
        {
            host.Children.Add(new MenuItem { Header = "显示刻度尺", ToggleType = MenuItemToggleType.CheckBox, IsChecked = true });
            host.Children.Add(new MenuItem { Header = "显示网格", ToggleType = MenuItemToggleType.CheckBox });
            host.Children.Add(new MenuItem { Header = "-" });
            var sub = new MenuItem { Header = "导出为" };
            foreach (var f in new[] { "PNG", "TIFF", "CSV" }) sub.Items.Add(new MenuItem { Header = f });
            host.Children.Add(sub);
        }
        return host;
    }

    static Control RightClickTarget()
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "复制", InputGesture = new KeyGesture(Key.C, KeyModifiers.Control) });
        menu.Items.Add(new MenuItem { Header = "粘贴", InputGesture = new KeyGesture(Key.V, KeyModifiers.Control) });
        menu.Items.Add(new MenuItem { Header = "-" });
        menu.Items.Add(new MenuItem { Header = "属性…" });
        return new Border
        {
            Width = 180, Height = 60,
            BorderThickness = new global::Avalonia.Thickness(1),
            BorderBrush = global::Avalonia.Media.Brushes.Gray,
            ContextMenu = menu,
            Child = new TextBlock { Text = "在这里右键", HorizontalAlignment = HorizontalAlignment.Center,
                                    VerticalAlignment = VerticalAlignment.Center },
        };
    }

    /// <summary>塞一段够长的内容，让滚动条真的出现。</summary>
    static ScrollViewer Scrollable(ScrollViewer sv, bool wide = false)
    {
        var sp = new StackPanel();
        for (var i = 1; i <= 12; i++)
            sp.Children.Add(new TextBlock
            {
                Text = wide ? $"第 {i} 行 —— 这一行有意写得很长，用来把横向滚动条也逼出来" : $"第 {i} 行",
                Margin = new global::Avalonia.Thickness(4, 3),
            });
        sv.Content = sp;
        return sv;
    }

    static ComboBox Items(ComboBox cb)
    {
        foreach (var s in new[] { "Mono8", "Mono12", "BayerRG8" })
            cb.Items.Add(new ComboBoxItem { Content = s });
        return cb;
    }
}
