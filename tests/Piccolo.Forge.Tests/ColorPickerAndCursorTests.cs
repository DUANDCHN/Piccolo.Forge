using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia.ColorPicker;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 验证 ColorPicker 新版样式（触发按钮方案 A + 调色板/预览/分量/棋盘格）以及全套控件的 Cursor 与悬停策略。
/// </summary>
public class ColorPickerAndCursorTests
{
    private static IDisposable MountColorPickerTheme()
    {
        var styles = Application.Current!.Styles;
        var theme = new ForgeColorPickerTheme();
        styles.Add(theme);
        return new ActionDisposable(() => styles.Remove(theme));
    }

    private sealed class ActionDisposable(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }

    private static void Relayout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static Point Center(Control c) => new(c.Bounds.Width / 2, c.Bounds.Height / 2);

    private static Color? ResourceColor(Control c, string key)
        => c.TryFindResource(key, c.ActualThemeVariant, out var v) && v is ISolidColorBrush b ? b.Color : null;

    [AvaloniaTheory]
    [InlineData(ForgeThemeFamily.ExpUi)]
    [InlineData(ForgeThemeFamily.Islands)]
    public void ColorPicker_外部触发器与ComboBox等高且展示色块Hex透明度与箭头(ForgeThemeFamily family)
    {
        using var theme = ForgeThemeScope.Use(family);
        using var cpTheme = MountColorPickerTheme();

        var combo = new ComboBox
        {
            ItemsSource = new[] { "中" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var picker = new ColorPicker
        {
            Color = Color.Parse("#8CFF3B30"),
            IsAlphaVisible = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("120,200"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
        };
        Grid.SetColumn(combo, 1);
        Grid.SetRow(picker, 1);
        Grid.SetColumn(picker, 1);
        grid.Children.Add(combo);
        grid.Children.Add(picker);

        var window = new Window { Content = grid, Width = 360, Height = 200 };
        window.Show();
        Relayout(window);

        try
        {
            Assert.Equal(combo.Bounds.Height, picker.Bounds.Height, 1);
            Assert.Equal(combo.Bounds.Width, picker.Bounds.Width, 1);

            var toggle = picker.GetVisualDescendants().OfType<ToggleButton>().First(t => t.Name == "PART_ToggleButton");
            var swatch = toggle.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_CurrentColor");
            var hexText = toggle.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_HexDisplayText");
            var alphaBadge = toggle.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_AlphaBadge");
            var chevron = toggle.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Path>().First(p => p.Name == "PART_Chevron");

            Assert.Equal("Arrow", toggle.Cursor?.ToString());
            Assert.Equal("#FF3B30", hexText.Text);
            Assert.True(alphaBadge.IsVisible);
            Assert.Equal("55%", Assert.IsType<TextBlock>(alphaBadge.Child).Text);
            Assert.Equal(Color.Parse("#8CFF3B30"), Assert.IsAssignableFrom<ISolidColorBrush>(swatch.Background).Color);
            Assert.True(chevron.IsVisible);

            // 悬停时外框变为悬停色（与 ComboBox 同口径）
            window.MouseMove(toggle.TranslatePoint(Center(toggle), window)!.Value);
            Relayout(window);

            var root = toggle.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Root");
            Assert.Equal(ResourceColor(toggle, "Forge.Input.HoverBorder"), (root.BorderBrush as ISolidColorBrush)?.Color);

            // 打开 Popup：外框变为 FocusedBorder，内嵌 ColorView 带 40 空调色板且选色可回传到外部文本
            toggle.IsChecked = true;
            Relayout(window);

            Assert.Equal(ResourceColor(toggle, "Forge.Input.FocusedBorder"), (root.BorderBrush as ISolidColorBrush)?.Color);

            var popup = picker.GetVisualDescendants().OfType<Popup>().First(p => p.Name == "PART_Popup");
            var popupRoot = Assert.IsAssignableFrom<Control>(popup.Child);
            var embeddedView = popupRoot.GetVisualDescendants().OfType<ColorView>().Single();
            Assert.NotNull(embeddedView.PaletteColors);
            Assert.Equal(40, embeddedView.PaletteColors.Count());

            var thirdAlpha = embeddedView.GetVisualDescendants().OfType<ColorSlider>().First(s => s.Name == "SpectrumAlphaSlider");
            Assert.Equal(Orientation.Vertical, thirdAlpha.Orientation);
            Assert.True(thirdAlpha.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Checker").IsVisible);

            embeddedView.Color = Color.Parse("#FF3574F0");
            Relayout(window);
            Assert.Equal("#3574F0", hexText.Text);
            Assert.Equal("100%", Assert.IsType<TextBlock>(alphaBadge.Child).Text);

            // 隐藏 Alpha 时同步隐藏透明度角标
            picker.IsAlphaVisible = false;
            Relayout(window);
            Assert.False(alphaBadge.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(ForgeThemeFamily.ExpUi)]
    [InlineData(ForgeThemeFamily.Islands)]
    public void ColorView_调色板默认不为空且Previewer不再留白(ForgeThemeFamily family)
    {
        using var theme = ForgeThemeScope.Use(family);
        using var cpTheme = MountColorPickerTheme();

        var view = new ColorView
        {
            Color = Colors.Coral,
            IsAccentColorsVisible = true,
        };
        var window = new Window { Content = view, Width = 340, Height = 420 };
        window.Show();
        Relayout(window);

        try
        {
            // 切换到“调色板”页并点击第 6 格（#FF3574F0）—— 验证真实点击可选色
            var paletteTabControl = view.GetVisualDescendants().OfType<TabControl>().First(t => t.Name == "PART_TabControl");
            paletteTabControl.SelectedIndex = 1;
            Relayout(window);

            var paletteList = view.GetVisualDescendants().OfType<ListBox>().First(l => l.Name == "PART_PaletteListBox");
            var ug = Assert.IsType<UniformGrid>(paletteList.ItemsPanelRoot);
            var items = paletteList.GetVisualDescendants().OfType<ListBoxItem>().ToArray();
            var targetItem = items[5];
            var clickPt = targetItem.TranslatePoint(Center(targetItem), window)!.Value;
            window.MouseDown(clickPt, MouseButton.Left);
            window.MouseUp(clickPt, MouseButton.Left);
            Relayout(window);

            Assert.Equal(5, paletteList.SelectedIndex);
            Assert.Equal(1, view.SelectedIndex);
            Assert.Equal(Color.Parse("#FF3574F0"), view.Color);

            // 默认自带 8x5=40 空调色板，不再空白
            Assert.NotNull(view.Palette);
            Assert.Equal(8, view.PaletteColumnCount);
            Assert.NotNull(view.PaletteColors);
            Assert.Equal(40, view.PaletteColors.Count());

            var previewer = view.GetVisualDescendants().OfType<ColorPreviewer>().Single();
            var inc1 = previewer.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_AccentIncrement1Border");
            var dec1 = previewer.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_AccentDecrement1Border");
            var cur = previewer.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_CurrentColorBorder");

            Assert.True(inc1.IsVisible);
            Assert.True(dec1.IsVisible);
            Assert.IsAssignableFrom<ISolidColorBrush>(inc1.Background);
            Assert.IsAssignableFrom<ISolidColorBrush>(dec1.Background);
            Assert.Equal(2, Grid.GetColumn(cur));
            Assert.Equal(1, Grid.GetColumnSpan(cur));

            // 关闭深浅变体后当前色铺满整条预览框（跨满 5 列）
            view.IsAccentColorsVisible = false;
            Relayout(window);

            Assert.False(inc1.IsVisible);
            Assert.Equal(0, Grid.GetColumn(cur));
            Assert.Equal(5, Grid.GetColumnSpan(cur));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ColorView_分量标签跟随模型切换且Alpha显示棋盘格()
    {
        using var theme = ForgeThemeScope.Use(ForgeThemeFamily.Islands);
        using var cpTheme = MountColorPickerTheme();

        var view = new ColorView
        {
            Color = Color.FromArgb(128, 53, 116, 240),
            ColorModel = ColorModel.Rgba,
        };
        var window = new Window { Content = view, Width = 340, Height = 420 };
        window.Show();
        Relayout(window);

        try
        {
            var tabControl = view.GetVisualDescendants().OfType<TabControl>().First(t => t.Name == "PART_TabControl");
            // 切到“分量”页使模板实例化
            tabControl.SelectedIndex = 2;
            Relayout(window);

            var c1Label = view.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "Comp1Label");
            var c2Label = view.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "Comp2Label");
            var c3Label = view.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "Comp3Label");
            var aLabel = view.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "AlphaLabel");
            Assert.Equal("R", c1Label.Text);
            Assert.Equal("G", c2Label.Text);
            Assert.Equal("B", c3Label.Text);
            Assert.Equal("A", aLabel.Text);

            var comp1Slider = view.GetVisualDescendants().OfType<ColorSlider>().First(s => s.Name == "Comp1Slider");
            var alphaSlider = view.GetVisualDescendants().OfType<ColorSlider>().First(s => s.Name == "AlphaSlider");
            Assert.False(comp1Slider.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Checker").IsVisible);
            Assert.True(alphaSlider.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Checker").IsVisible);

            // 切换到 HSV 模型，标签自动变 H/S/V/A
            var hsvRadio = view.GetVisualDescendants().OfType<RadioButton>().First(r => r.Name == "HsvModelRadio");
            hsvRadio.IsChecked = true;
            Relayout(window);

            Assert.Equal(ColorModel.Hsva, view.ColorModel);
            Assert.Equal("H", c1Label.Text);
            Assert.Equal("S", c2Label.Text);
            Assert.Equal("V", c3Label.Text);
            Assert.Equal("A", aLabel.Text);

            // 点击 25% 透明快捷预设按钮
            var btn25 = view.GetVisualDescendants().OfType<Button>().First(b => Equals(b.Content, "25%"));
            btn25.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Relayout(window);
            Assert.InRange(view.Color.A, 63, 65);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(ForgeThemeFamily.ExpUi)]
    [InlineData(ForgeThemeFamily.Islands)]
    public void 交互控件光标规范与ToggleSwitch悬停反馈生效(ForgeThemeFamily family)
    {
        using var theme = ForgeThemeScope.Use(family);
        using var cpTheme = MountColorPickerTheme();

        var toggleSwitch = new ToggleSwitch { OffContent = "", OnContent = "" };
        var button = new Button { Content = "确定" };
        var iconButton = new Button { Content = "×", Classes = { "icon" } };
        var combo = new ComboBox { ItemsSource = new[] { "A" }, SelectedIndex = 0 };
        var check = new CheckBox { Content = "勾选" };
        var radio = new RadioButton { Content = "单选" };
        var link = new HyperlinkButton { Content = "链接" };
        var tabItem = new TabItem { Header = "页签" };
        var tabControl = new TabControl { Items = { tabItem } };
        var expander = new Expander { Header = "折叠组" };
        var colorPicker = new ColorPicker();

        var stack = new StackPanel
        {
            Children = { toggleSwitch, button, iconButton, combo, check, radio, link, tabControl, expander, colorPicker },
        };
        var window = new Window { Content = stack, Width = 400, Height = 500 };
        window.Show();
        Relayout(window);

        try
        {
            static string? CursorName(Control c) => c.Cursor?.ToString();

            // 即时开关 / 无边框 / 导航页签 / 折叠头 -> Hand
            Assert.Equal("Hand", CursorName(toggleSwitch));
            Assert.Equal("Hand", CursorName(iconButton));
            Assert.Equal("Hand", CursorName(link));
            Assert.Equal("Hand", CursorName(tabItem));
            var expanderHeader = expander.GetVisualDescendants().OfType<ToggleButton>().First(b => b.Name == "PART_HeaderButton");
            Assert.Equal("Hand", CursorName(expanderHeader));

            // 常规表单框控件 -> Arrow
            Assert.Equal("Arrow", CursorName(button));
            Assert.Equal("Arrow", CursorName(combo));
            Assert.Equal("Arrow", CursorName(check));
            Assert.Equal("Arrow", CursorName(radio));
            var cpButton = colorPicker.GetVisualDescendants().OfType<ToggleButton>().First(b => b.Name == "PART_ToggleButton");
            Assert.Equal("Arrow", CursorName(cpButton));

            // ToggleSwitch 悬停必须点亮边框和叠加层
            var track = toggleSwitch.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_SwitchTrack");
            var overlay = toggleSwitch.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_TrackOverlay");
            Assert.Equal(Colors.Transparent, (track.BorderBrush as ISolidColorBrush)?.Color);
            Assert.Equal(Colors.Transparent, (overlay.Background as ISolidColorBrush)?.Color);

            window.MouseMove(track.TranslatePoint(Center(track), window)!.Value);
            Relayout(window);

            Assert.True(toggleSwitch.IsPointerOver);
            Assert.Equal(ResourceColor(toggleSwitch, "Forge.Check.SelectedBorder"), (track.BorderBrush as ISolidColorBrush)?.Color);
            Assert.Equal(ResourceColor(toggleSwitch, "Forge.Overlay.Hover"), (overlay.Background as ISolidColorBrush)?.Color);
        }
        finally
        {
            window.Close();
        }
    }
}
