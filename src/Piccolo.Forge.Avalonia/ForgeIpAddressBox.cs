using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 四段居中分格 IPv4 输入控件：统一外框内划分为 4 个居中数字格，以居中圆点分隔，
/// 支持点号跳格、输满 3 位自动进格、↑/↓ 微调、Backspace/方向键跨段导航与整串 IP 粘贴拆填。
/// </summary>
[TemplatePart("PART_Octet0", typeof(TextBox))]
[TemplatePart("PART_Octet1", typeof(TextBox))]
[TemplatePart("PART_Octet2", typeof(TextBox))]
[TemplatePart("PART_Octet3", typeof(TextBox))]
[PseudoClasses(":error")]
public partial class ForgeIpAddressBox : TemplatedControl
{
    private const int SegmentCount = 4;

    /// <summary><see cref="Text"/> 的依赖属性（默认双向绑定并支持数据校验）。</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ForgeIpAddressBox, string?>(
            nameof(Text),
            defaultBindingMode: BindingMode.TwoWay,
            enableDataValidation: true);

    /// <summary><see cref="SegmentPlaceholder"/> 的依赖属性。</summary>
    public static readonly StyledProperty<string?> SegmentPlaceholderProperty =
        AvaloniaProperty.Register<ForgeIpAddressBox, string?>(
            nameof(SegmentPlaceholder),
            defaultValue: "---");

    /// <summary><see cref="ClampOctets"/> 的依赖属性。</summary>
    public static readonly StyledProperty<bool> ClampOctetsProperty =
        AvaloniaProperty.Register<ForgeIpAddressBox, bool>(nameof(ClampOctets), defaultValue: true);

    private readonly ForgeSegmentGroup _segments;
    private bool _syncing;

    /// <summary>初始化四段 IPv4 输入框。</summary>
    public ForgeIpAddressBox()
    {
        _segments = new ForgeSegmentGroup(
            SegmentCount,
            _ => new ForgeSegmentSpec(MaxLength: 3, MinValue: 0, MaxValue: ClampOctets ? 255 : 999, WrapOnStep: false, PadWithZero: false),
            ['.'],
            [Key.OemPeriod, Key.Decimal],
            CommitSegmentsToText);
    }

    /// <summary>完整点分十进制文本（如 <c>"192.168.1.100"</c>）；四段均为空时返回 <c>null</c> 或空串。</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>各空分段内显示的居中淡色占位符，默认 <c>"---"</c>。</summary>
    public string? SegmentPlaceholder
    {
        get => GetValue(SegmentPlaceholderProperty);
        set => SetValue(SegmentPlaceholderProperty, value);
    }

    /// <summary>是否只读。</summary>
    [GeneratedStyledProperty]
    public partial bool IsReadOnly { get; set; }

    /// <summary>是否自动将超过 255 的单段数值钳位到 255，默认 <c>true</c>。</summary>
    public bool ClampOctets
    {
        get => GetValue(ClampOctetsProperty);
        set => SetValue(ClampOctetsProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        _segments.Detach();
        base.OnApplyTemplate(e);
        _segments.Attach(e.NameScope, "PART_Octet");
        SyncSegmentsFromText();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty && !_syncing)
            SyncSegmentsFromText();
        else if (change.Property == ClampOctetsProperty && ClampOctets)
        {
            _segments.NormalizeAll(enforceMin: false);
            CommitSegmentsToText();
        }
    }

    /// <inheritdoc />
    protected override void UpdateDataValidation(AvaloniaProperty property, BindingValueType state, Exception? error)
    {
        base.UpdateDataValidation(property, state, error);
        if (property == TextProperty)
            PseudoClasses.Set(":error", state == BindingValueType.DataValidationError);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.Handled || !IsEnabled || _segments.HasAnyFocusedSegment())
            return;

        _segments.FocusPreferredEmptyOrFirst();
        e.Handled = true;
    }

    private void SyncSegmentsFromText()
    {
        var value = Text;
        var parts = string.IsNullOrWhiteSpace(value) ? [] : value.Split('.');
        _segments.SetSegmentValues(parts);
    }

    private void CommitSegmentsToText()
    {
        var values = new string[SegmentCount];
        var any = false;
        for (var i = 0; i < SegmentCount; i++)
        {
            var t = _segments.GetRawText(i);
            values[i] = t;
            if (t.Length > 0)
                any = true;
        }

        var nextText = any ? string.Join('.', values) : string.Empty;
        _syncing = true;
        try
        {
            SetCurrentValue(TextProperty, nextText);
        }
        finally
        {
            _syncing = false;
        }
    }
}
