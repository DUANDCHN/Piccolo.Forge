using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 四段居中分格 IPv4 输入控件：统一外框内划分为 4 个居中数字格，以居中圆点分隔，
/// 支持点号跳格、输满 3 位自动进格、Backspace/方向键跨段导航与整串 IP 粘贴拆填。
/// </summary>
[TemplatePart("PART_Octet0", typeof(TextBox))]
[TemplatePart("PART_Octet1", typeof(TextBox))]
[TemplatePart("PART_Octet2", typeof(TextBox))]
[TemplatePart("PART_Octet3", typeof(TextBox))]
[PseudoClasses(":error")]
public class ForgeIpAddressBox : TemplatedControl
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

    /// <summary><see cref="IsReadOnly"/> 的依赖属性。</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<ForgeIpAddressBox, bool>(nameof(IsReadOnly));

    /// <summary><see cref="ClampOctets"/> 的依赖属性。</summary>
    public static readonly StyledProperty<bool> ClampOctetsProperty =
        AvaloniaProperty.Register<ForgeIpAddressBox, bool>(nameof(ClampOctets), defaultValue: true);

    private readonly TextBox?[] _segments = new TextBox?[SegmentCount];
    private bool _syncing;

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
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>是否自动将超过 255 的单段数值钳位到 255，默认 <c>true</c>。</summary>
    public bool ClampOctets
    {
        get => GetValue(ClampOctetsProperty);
        set => SetValue(ClampOctetsProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        DetachSegments();
        base.OnApplyTemplate(e);

        for (var i = 0; i < SegmentCount; i++)
        {
            var box = e.NameScope.Find<TextBox>($"PART_Octet{i}");
            _segments[i] = box;
            if (box is null)
                continue;

            var index = i;
            box.Tag = index;
            box.TextChanged += OnSegmentTextChanged;
            box.AddHandler(KeyDownEvent, OnSegmentKeyDown, RoutingStrategies.Tunnel);
            box.AddHandler(TextInputEvent, OnSegmentTextInput, RoutingStrategies.Tunnel);
        }

        SyncSegmentsFromText();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty && !_syncing)
            SyncSegmentsFromText();
        else if (change.Property == ClampOctetsProperty && ClampOctets)
            NormalizeAllSegments();
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

        // 点击分隔点或边框留白时自动把焦点引到第一个未填段（均已填时引到首段）。
        if (e.Handled || !IsEnabled)
            return;

        if (_segments.Any(s => s is not null && s.IsFocused))
            return;

        var target = _segments.FirstOrDefault(s => s is not null && string.IsNullOrEmpty(s.Text))
                     ?? _segments[0];
        target?.Focus();
        e.Handled = true;
    }

    private void DetachSegments()
    {
        for (var i = 0; i < SegmentCount; i++)
        {
            var box = _segments[i];
            if (box is null)
                continue;

            box.TextChanged -= OnSegmentTextChanged;
            box.RemoveHandler(KeyDownEvent, OnSegmentKeyDown);
            box.RemoveHandler(TextInputEvent, OnSegmentTextInput);
            _segments[i] = null;
        }
    }

    private void OnSegmentTextInput(object? sender, TextInputEventArgs e)
    {
        if (sender is not TextBox box || box.Tag is not int index)
            return;

        var text = e.Text;
        if (string.IsNullOrEmpty(text))
            return;

        // 支持直接键入 '.' 跳到下一段。
        if (text == ".")
        {
            if (!string.IsNullOrEmpty(box.Text) && index < SegmentCount - 1)
                FocusSegment(index + 1, selectAll: true);
            e.Handled = true;
            return;
        }

        // 仅允许数字或包含点分四段的粘贴字符串；其余字符直接拦截。
        if (!text.Contains('.') && !text.All(char.IsAsciiDigit))
            e.Handled = true;
    }

    private void OnSegmentKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || box.Tag is not int index)
            return;

        if (e.Key is Key.OemPeriod or Key.Decimal)
        {
            if (!string.IsNullOrEmpty(box.Text) && index < SegmentCount - 1)
                FocusSegment(index + 1, selectAll: true);
            e.Handled = true;
            return;
        }

        var len = box.Text?.Length ?? 0;
        var hasSelection = box.SelectionStart != box.SelectionEnd;

        if (e.Key == Key.Back && !hasSelection && box.CaretIndex == 0 && index > 0)
        {
            var prev = _segments[index - 1];
            if (prev is not null)
            {
                prev.Focus();
                prev.CaretIndex = prev.Text?.Length ?? 0;
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Left && !hasSelection && box.CaretIndex == 0 && index > 0)
        {
            var prev = _segments[index - 1];
            if (prev is not null)
            {
                prev.Focus();
                prev.CaretIndex = prev.Text?.Length ?? 0;
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Right && !hasSelection && box.CaretIndex >= len && index < SegmentCount - 1)
        {
            var next = _segments[index + 1];
            if (next is not null)
            {
                next.Focus();
                next.CaretIndex = 0;
            }
            e.Handled = true;
        }
    }

    private void OnSegmentTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing || sender is not TextBox box || box.Tag is not int index)
            return;

        var raw = box.Text ?? string.Empty;

        // 若粘贴了完整点分串（如 "192.168.1.100"），自动拆分填满四段。
        if (raw.Contains('.'))
        {
            ApplyDottedText(raw, index);
            return;
        }

        var normalized = NormalizeSegmentDigits(raw);
        if (normalized != raw)
        {
            _syncing = true;
            try
            {
                box.Text = normalized;
                box.CaretIndex = normalized.Length;
            }
            finally
            {
                _syncing = false;
            }
        }

        CommitSegmentsToText();

        // 当前段填满 3 位且光标处于末尾时，自动跳入下一段提升连续输入效率。
        if (normalized.Length == 3 && box.CaretIndex >= 3 && index < SegmentCount - 1 && box.IsFocused)
            FocusSegment(index + 1, selectAll: true);
    }

    private void ApplyDottedText(string dotted, int startIndex)
    {
        var parts = dotted.Split('.');
        _syncing = true;
        try
        {
            var targetStart = parts.Length >= SegmentCount ? 0 : startIndex;
            for (var p = 0; p < parts.Length && targetStart + p < SegmentCount; p++)
            {
                var seg = _segments[targetStart + p];
                if (seg is not null)
                    seg.Text = NormalizeSegmentDigits(parts[p]);
            }
        }
        finally
        {
            _syncing = false;
        }

        CommitSegmentsToText();

        var focusIdx = Math.Min(SegmentCount - 1, (parts.Length >= SegmentCount ? 0 : startIndex) + parts.Length - 1);
        FocusSegment(focusIdx, selectAll: false);
    }

    private string NormalizeSegmentDigits(string input)
    {
        var digits = new string(input.Where(char.IsAsciiDigit).Take(3).ToArray());
        if (ClampOctets && digits.Length > 0 && int.TryParse(digits, out var val) && val > 255)
            return "255";

        return digits;
    }

    private void NormalizeAllSegments()
    {
        _syncing = true;
        try
        {
            foreach (var box in _segments)
            {
                if (box is not null && !string.IsNullOrEmpty(box.Text))
                    box.Text = NormalizeSegmentDigits(box.Text);
            }
        }
        finally
        {
            _syncing = false;
        }

        CommitSegmentsToText();
    }

    private void SyncSegmentsFromText()
    {
        _syncing = true;
        try
        {
            var value = Text;
            var parts = string.IsNullOrWhiteSpace(value) ? [] : value.Split('.');
            for (var i = 0; i < SegmentCount; i++)
            {
                var box = _segments[i];
                if (box is null)
                    continue;

                box.Text = i < parts.Length ? NormalizeSegmentDigits(parts[i]) : string.Empty;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void CommitSegmentsToText()
    {
        var values = new string[SegmentCount];
        var any = false;
        for (var i = 0; i < SegmentCount; i++)
        {
            var t = _segments[i]?.Text ?? string.Empty;
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

    private void FocusSegment(int index, bool selectAll)
    {
        var target = _segments[index];
        if (target is null)
            return;

        target.Focus();
        if (selectAll)
            target.SelectAll();
        else
            target.CaretIndex = target.Text?.Length ?? 0;
    }
}
