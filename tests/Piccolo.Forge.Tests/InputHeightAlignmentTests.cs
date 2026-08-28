using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 一排参数控件必须**一样高**：按钮 / 下拉框 / 文本框 / 数字框摆在同一行时，
/// 谁高一档谁矮一档都会让面板看起来是拼出来的。
/// </summary>
/// <remarks>
/// 这条盯的是"控件之间"的一致性，不是某个具体数值 —— 所以断言写成"都等于按钮的高"，
/// 而不是硬编码 28：主题把这一档调大调小都行，但四个必须一起动。
/// <para>
/// 数字框是这里最容易破的一个：它的模板套着 ButtonSpinner，里头还嵌一个 TextBox，
/// 三层各有各的最小高度与内边距，任何一层多给一点都会把整条顶高（实测过）。
/// </para>
/// </remarks>
public class InputHeightAlignmentTests
{
    /// <summary>照参数面板的实际摆法（拉伸列 + 框内单位）在 Islands 档下比一遍。</summary>
    [AvaloniaFact]
    public void Numeric_matches_combobox_in_a_stretched_param_row_islands()
    {
        using var _ = ForgeThemeScope.Use(Themes.ForgeThemeFamily.Islands);

        var combo = new ComboBox
        {
            ItemsSource = new[] { "关闭" }, SelectedIndex = 0, FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var numeric = new NumericUpDown
        {
            Value = 10000, FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            InnerRightContent = new TextBlock { Text = "us", VerticalAlignment = VerticalAlignment.Center },
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("100,*"), RowDefinitions = new RowDefinitions("Auto,Auto") };
        Grid.SetColumn(combo, 1);
        Grid.SetRow(numeric, 1);
        Grid.SetColumn(numeric, 1);
        grid.Children.Add(combo);
        grid.Children.Add(numeric);

        var window = new Window { Content = grid, Width = 320, Height = 200 };
        window.Show();
        window.UpdateLayout();

        // 四个控件在**同一行的用法下**必须一样高：这里不断言某个具体数值，
        // 只断言它们相等 —— 主题把这一档调大调小都行，但四个要一起动。
        var btn = new Button { Content = "执行", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Stretch };
        var tb = new TextBox { Text = "Cam-01", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Stretch };
        var g2 = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto") };
        g2.Children.Add(btn);
        Grid.SetRow(tb, 1);
        g2.Children.Add(tb);
        var w2 = new Window { Content = g2, Width = 320, Height = 200 };
        w2.Show();
        w2.UpdateLayout();

        Assert.Equal(combo.Bounds.Height, numeric.Bounds.Height, 1);
        Assert.Equal(combo.Bounds.Height, tb.Bounds.Height, 1);
        Assert.Equal(combo.Bounds.Height, btn.Bounds.Height, 1);
    }

    [AvaloniaFact]
    public void Button_combobox_textbox_and_numeric_share_one_height()
    {
        var button = new Button { Content = "确定" };
        var combo = new ComboBox { ItemsSource = new[] { "单帧", "连续" }, SelectedIndex = 0 };
        var text = new TextBox { Text = "10000" };
        var numeric = new NumericUpDown { Value = 10000 };

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var c in new Control[] { button, combo, text, numeric }) row.Children.Add(c);

        var window = new Window { Content = row, Width = 600, Height = 200 };
        window.Show();
        window.UpdateLayout();

        var expected = button.Bounds.Height;
        Assert.Equal(expected, combo.Bounds.Height, 1);
        Assert.Equal(expected, text.Bounds.Height, 1);
        Assert.Equal(expected, numeric.Bounds.Height, 1);
    }

    /// <summary>
    /// 数字框带**框内单位**（InnerRightContent，参数面板拿它写 fps / μs）时高度不该变。
    /// </summary>
    [AvaloniaFact]
    public void Numeric_with_inner_unit_keeps_its_height()
    {
        var plain = new ComboBox { ItemsSource = new[] { "关闭" }, SelectedIndex = 0, FontSize = 13 };
        var numeric = new NumericUpDown
        {
            Value = 10000,
            FontSize = 13,
            InnerRightContent = new TextBlock { Text = "μs", VerticalAlignment = VerticalAlignment.Center },
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(plain);
        row.Children.Add(numeric);

        var window = new Window { Content = row, Width = 600, Height = 200 };
        window.Show();
        window.UpdateLayout();

        Assert.Equal(plain.Bounds.Height, numeric.Bounds.Height, 1);
    }

    /// <summary>
    /// 数字框摆在 <c>HorizontalAlignment=Stretch</c> 的格子里（参数面板就是这么用的）时，
    /// 高度不该跟着变 —— 拉宽只该改宽。
    /// </summary>
    [AvaloniaFact]
    public void Numeric_keeps_its_height_when_stretched()
    {
        var numeric = new NumericUpDown { Value = 10000, HorizontalAlignment = HorizontalAlignment.Stretch };
        var combo = new ComboBox
        {
            ItemsSource = new[] { "关闭" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto") };
        grid.Children.Add(combo);
        Grid.SetRow(numeric, 1);
        grid.Children.Add(numeric);

        var window = new Window { Content = grid, Width = 300, Height = 200 };
        window.Show();
        window.UpdateLayout();

        Assert.Equal(combo.Bounds.Height, numeric.Bounds.Height, 1);
    }

    /// <summary>把画出来的字那一层挪到控件坐标里，取左起点。</summary>
    private static double TextLeft(Control host)
    {
        Control text = host.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault()
                       ?? (Control?)host.GetVisualDescendants().OfType<TextBlock>()
                              .FirstOrDefault(t => !string.IsNullOrEmpty(t.Text))
                       ?? throw new Xunit.Sdk.XunitException($"{host.GetType().Name} 里没找到画字的那一层");
        return text.TranslatePoint(default, host)?.X ?? throw new Xunit.Sdk.XunitException("取不到坐标");
    }

    /// <summary>
    /// 一排参数控件的字要**从同一条竖线起笔**：下拉框与数字框左边差一截，
    /// 整列读起来就是错位的。
    /// </summary>
    /// <remarks>
    /// 破过一次（2026-08-28 宿主实机）：TextBox 模板里两个内嵌槽（前缀 / 后缀）
    /// 没内容时宽为 0，Margin 却照样吃掉 8px —— 一个什么前缀都没有的数字框，
    /// 字比同行 ComboBox 右移 8px（实测 x=17 对 x=10）。修法是空槽 <c>IsVisible=false</c>，
    /// 不占位；光把宽度做成 0 没用。
    /// <para>
    /// 容差 1px 是**有出处的**：ComboBox 的左内边距照上游是 9，输入类是 8，两边各自忠于上游、
    /// 不为了这 1px 去改任何一侧的档位。这条盯的是"别再差出一整个 8px"。
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void Combobox_and_numeric_start_their_text_at_the_same_left_edge()
    {
        var combo = new ComboBox { ItemsSource = new[] { "13" }, SelectedIndex = 0, Width = 96 };
        var numeric = new NumericUpDown { Value = 100, FormatString = "0", Width = 96 };
        var text = new TextBox { Text = "100", Width = 96 };

        var stack = new StackPanel();
        stack.Children.Add(combo);
        stack.Children.Add(numeric);
        stack.Children.Add(text);

        var window = new Window { Content = stack, Width = 320, Height = 200 };
        window.Show();
        window.UpdateLayout();

        // ↓ 写成“差不超过 1px”而不是 Assert.Equal(…, 1)：后者的 1 是**小数位**、不是容差。
        Assert.InRange(System.Math.Abs(TextLeft(combo) - TextLeft(numeric)), 0, 1);
        Assert.InRange(System.Math.Abs(TextLeft(combo) - TextLeft(text)), 0, 1);
    }
}
