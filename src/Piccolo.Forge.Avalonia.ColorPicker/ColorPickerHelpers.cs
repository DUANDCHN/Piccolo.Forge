using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace Piccolo.Forge.Avalonia.ColorPicker;

/// <summary>
/// 取色器主题的内置默认调色板（8 列 × 5 行 = 40 色）。
/// </summary>
/// <remarks>
/// 第 0~2 行为鲜明标准色及其浅/深变体，第 3 行为白至黑中性灰阶，第 4 行为半透明叠层常用色；
/// 宿主仍可通过 <see cref="ColorView.Palette"/> 覆盖。
/// </remarks>
internal sealed class ForgeDefaultColorPalette : IColorPalette
{
    // 按列组织（8 列 × 5 档），让 GetColor(colorIndex, shadeIndex) 与 8 列 UniformGrid 的视觉行一致。
    private static readonly Color[,] Grid =
    {
        // 列 0: 红
        {
            Color.Parse("#FFFF3B30"),
            Color.Parse("#FFFF6961"),
            Color.Parse("#FFB91C1C"),
            Color.Parse("#FFFFFFFF"),
            Color.Parse("#8CFF3B30"),
        },
        // 列 1: 橙
        {
            Color.Parse("#FFFF9500"),
            Color.Parse("#FFFFB340"),
            Color.Parse("#FFC2410C"),
            Color.Parse("#FFE5E7EB"),
            Color.Parse("#8CFF9500"),
        },
        // 列 2: 黄
        {
            Color.Parse("#FFFFCC00"),
            Color.Parse("#FFFFE066"),
            Color.Parse("#FFA16207"),
            Color.Parse("#FFCBD5E1"),
            Color.Parse("#8CFFCC00"),
        },
        // 列 3: 绿
        {
            Color.Parse("#FF34C759"),
            Color.Parse("#FF63DA82"),
            Color.Parse("#FF15803D"),
            Color.Parse("#FF9CA3AF"),
            Color.Parse("#8C34C759"),
        },
        // 列 4: 青
        {
            Color.Parse("#FF00C7BE"),
            Color.Parse("#FF5AC8FA"),
            Color.Parse("#FF0F766E"),
            Color.Parse("#FF6B7280"),
            Color.Parse("#8C00C7BE"),
        },
        // 列 5: 蓝
        {
            Color.Parse("#FF3574F0"),
            Color.Parse("#FF6EA8FF"),
            Color.Parse("#FF1D4ED8"),
            Color.Parse("#FF374151"),
            Color.Parse("#8C3574F0"),
        },
        // 列 6: 靛蓝
        {
            Color.Parse("#FF5856D6"),
            Color.Parse("#FF8E8CD8"),
            Color.Parse("#FF4338CA"),
            Color.Parse("#FF1F2329"),
            Color.Parse("#40FFFFFF"),
        },
        // 列 7: 紫 / 黑
        {
            Color.Parse("#FFAF52DE"),
            Color.Parse("#FFDA8FFF"),
            Color.Parse("#FF7E22CE"),
            Color.Parse("#FF000000"),
            Color.Parse("#80000000"),
        },
    };

    /// <inheritdoc />
    public int ColorCount => Grid.GetLength(0);

    /// <inheritdoc />
    public int ShadeCount => Grid.GetLength(1);

    /// <inheritdoc />
    public Color GetColor(int colorIndex, int shadeIndex)
        => Grid[Math.Clamp(colorIndex, 0, ColorCount - 1), Math.Clamp(shadeIndex, 0, ShadeCount - 1)];
}

/// <summary>
/// 将 <see cref="Color"/> 格式化为触发按钮上的简短 <c>#RRGGBB</c> 文本（透明度另由角标展示，避免按钮文本拥挤）。
/// </summary>
internal sealed class ForgeRgbHexDisplayConverter : IValueConverter
{
    public static readonly ForgeRgbHexDisplayConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Color color ? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : "#000000";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => AvaloniaProperty.UnsetValue;
}

/// <summary>
/// 将 <see cref="Color.A"/> 转为触发按钮上的百分比角标文本（如 <c>100%</c>、<c>55%</c>）。
/// </summary>
internal sealed class ForgeAlphaPercentConverter : IValueConverter
{
    public static readonly ForgeAlphaPercentConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Color color)
            return "100%";

        var pct = (int)Math.Round(color.A / 255.0 * 100.0);
        return $"{pct}%";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => AvaloniaProperty.UnsetValue;
}

/// <summary>
/// 将 <see cref="ColorModel"/> 枚举与参数字符串进行双向绑定，供 RGB / HSV 分段切换使用。
/// </summary>
internal sealed class ForgeColorModelEqualsConverter : IValueConverter
{
    public static readonly ForgeColorModelEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ColorModel model && parameter is string name && Enum.TryParse<ColorModel>(name, ignoreCase: true, out var target))
            return model == target;

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is string name && Enum.TryParse<ColorModel>(name, ignoreCase: true, out var target))
            return target;

        return AvaloniaProperty.UnsetValue;
    }
}

/// <summary>
/// 根据当前 <see cref="ColorModel"/> 与通道序号参数（"1"~"4"），输出分量行标签（R/G/B/A 或 H/S/V/A）。
/// </summary>
internal sealed class ForgeComponentLabelConverter : IValueConverter
{
    public static readonly ForgeComponentLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isHsv = value is ColorModel model && model == ColorModel.Hsva;
        return parameter?.ToString() switch
        {
            "1" => isHsv ? "H" : "R",
            "2" => isHsv ? "S" : "G",
            "3" => isHsv ? "V" : "B",
            "4" => "A",
            _ => string.Empty,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => AvaloniaProperty.UnsetValue;
}

/// <summary>
/// 分量页快捷透明度百分比附着行为（如 25%、50%、75%、100% 快捷芯片）。
/// </summary>
internal static class ForgeAlphaQuickPreset
{
    public static readonly AttachedProperty<int> PercentProperty =
        AvaloniaProperty.RegisterAttached<Button, int>("Percent", typeof(ForgeAlphaQuickPreset), defaultValue: -1);

    static ForgeAlphaQuickPreset()
    {
        PercentProperty.Changed.AddClassHandler<Button>((button, args) =>
        {
            button.Click -= OnButtonClick;
            if (args.NewValue is int pct and >= 0 and <= 100)
                button.Click += OnButtonClick;
        });
    }

    public static int GetPercent(Button element) => element.GetValue(PercentProperty);

    public static void SetPercent(Button element, int value) => element.SetValue(PercentProperty, value);

    private static void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        var pct = GetPercent(button);
        if (pct < 0)
            return;

        var colorView = button.FindLogicalAncestorOfType<ColorView>();
        if (colorView is null || !colorView.IsAlphaEnabled)
            return;

        var alpha = (byte)Math.Clamp((int)Math.Round(255.0 * pct / 100.0), 0, 255);
        var current = colorView.Color;
        colorView.Color = Color.FromArgb(alpha, current.R, current.G, current.B);
        e.Handled = true;
    }
}
