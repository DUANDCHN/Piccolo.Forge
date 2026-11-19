using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 单个分段输入框的校验、跳格与步进规则描述。
/// </summary>
internal readonly record struct ForgeSegmentSpec(
    int MaxLength,
    int MinValue,
    int MaxValue,
    bool WrapOnStep,
    bool PadWithZero,
    int? DynamicMaxOverride = null);

/// <summary>
/// 复用分段数字输入框（IP / 日期 / 时间）的按键过滤、点/斜杠/冒号跳段、
/// 方向键与 Backspace 跨段导航、粘贴拆填及上下键步进逻辑。
/// </summary>
internal sealed class ForgeSegmentGroup
{
    private readonly int _count;
    private readonly TextBox?[] _boxes;
    private readonly Func<int, ForgeSegmentSpec> _specProvider;
    private readonly HashSet<char> _jumpChars;
    private readonly HashSet<Key> _jumpKeys;
    private readonly Action _onCommitted;
    private readonly Action<int>? _onSegmentBlurred;

    private bool _syncing;

    public ForgeSegmentGroup(
        int count,
        Func<int, ForgeSegmentSpec> specProvider,
        IEnumerable<char> jumpChars,
        IEnumerable<Key> jumpKeys,
        Action onCommitted,
        Action<int>? onSegmentBlurred = null)
    {
        _count = count;
        _boxes = new TextBox?[count];
        _specProvider = specProvider;
        _jumpChars = [.. jumpChars];
        _jumpKeys = [.. jumpKeys];
        _onCommitted = onCommitted;
        _onSegmentBlurred = onSegmentBlurred;
    }

    public bool IsSyncing => _syncing;

    public void Attach(INameScope scope, string partPrefix)
    {
        Detach();
        for (var i = 0; i < _count; i++)
        {
            var box = scope.Find<TextBox>($"{partPrefix}{i}");
            _boxes[i] = box;
            if (box is null)
                continue;

            box.Tag = i;
            box.TextChanged += OnTextChanged;
            box.LostFocus += OnLostFocus;
            box.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
            box.AddHandler(InputElement.TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
        }
    }

    public void Detach()
    {
        for (var i = 0; i < _count; i++)
        {
            var box = _boxes[i];
            if (box is null)
                continue;

            box.TextChanged -= OnTextChanged;
            box.LostFocus -= OnLostFocus;
            box.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            box.RemoveHandler(InputElement.TextInputEvent, OnTextInput);
            _boxes[i] = null;
        }
    }

    public string GetRawText(int index)
        => index >= 0 && index < _count ? (_boxes[index]?.Text ?? string.Empty) : string.Empty;

    public int? GetInt(int index)
    {
        var raw = GetRawText(index);
        return raw.Length > 0 && int.TryParse(raw, out var value) ? value : null;
    }

    public void SetSegmentValues(IReadOnlyList<string> values)
    {
        _syncing = true;
        try
        {
            for (var i = 0; i < _count; i++)
            {
                var box = _boxes[i];
                if (box is null)
                    continue;

                var spec = _specProvider(i);
                var raw = i < values.Count ? values[i] : string.Empty;
                box.Text = ClampDigits(raw, spec, enforceMin: false);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    public void SetSegmentText(int index, string value)
    {
        if (index < 0 || index >= _count || _boxes[index] is not { } box)
            return;

        _syncing = true;
        try
        {
            box.Text = value;
        }
        finally
        {
            _syncing = false;
        }
    }

    public void RunWithSync(Action action)
    {
        _syncing = true;
        try
        {
            action();
        }
        finally
        {
            _syncing = false;
        }
    }

    public bool HasAnyFocusedSegment()
        => _boxes.Any(static b => b is not null && b.IsFocused);

    public void FocusPreferredEmptyOrFirst()
    {
        var target = _boxes.FirstOrDefault(static b => b is not null && b.IsVisible && string.IsNullOrEmpty(b.Text))
                     ?? _boxes.FirstOrDefault(static b => b is not null && b.IsVisible);
        target?.Focus();
    }

    public void NormalizeAll(bool enforceMin)
    {
        _syncing = true;
        try
        {
            for (var i = 0; i < _count; i++)
            {
                var box = _boxes[i];
                if (box is null || string.IsNullOrEmpty(box.Text))
                    continue;

                box.Text = ClampDigits(box.Text, _specProvider(i), enforceMin);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private int LastVisibleSegmentIndex()
    {
        for (var i = _count - 1; i >= 0; i--)
        {
            if (_boxes[i] is { IsVisible: true })
                return i;
        }

        return _count - 1;
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (sender is not TextBox box || box.Tag is not int index)
            return;

        var text = e.Text;
        if (string.IsNullOrEmpty(text))
            return;

        if (text.Length == 1 && _jumpChars.Contains(text[0]))
        {
            var last = LastVisibleSegmentIndex();
            if (!string.IsNullOrEmpty(box.Text) && index < last)
                FocusSegment(index + 1, selectAll: true);
            e.Handled = true;
            return;
        }

        // 允许包含分隔符的粘贴字符串交由 TextChanged 拆段；单次键盘输入仅放行 ASCII 数字。
        var hasSeparator = text.Any(c => _jumpChars.Contains(c));
        if (!hasSeparator && !text.All(char.IsAsciiDigit))
            e.Handled = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || box.Tag is not int index)
            return;

        var last = LastVisibleSegmentIndex();

        if (_jumpKeys.Contains(e.Key))
        {
            if (!string.IsNullOrEmpty(box.Text) && index < last)
                FocusSegment(index + 1, selectAll: true);
            e.Handled = true;
            return;
        }

        if (!box.IsReadOnly && e.Key is Key.Up or Key.Down)
        {
            StepSegment(box, index, e.Key == Key.Up ? 1 : -1);
            e.Handled = true;
            return;
        }

        var len = box.Text?.Length ?? 0;
        var hasSelection = box.SelectionStart != box.SelectionEnd;

        if (e.Key == Key.Back && !hasSelection && box.CaretIndex == 0 && index > 0)
        {
            FocusSegment(index - 1, selectAll: false);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Left && !hasSelection && box.CaretIndex == 0 && index > 0)
        {
            FocusSegment(index - 1, selectAll: false);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Right && !hasSelection && box.CaretIndex >= len && index < last)
        {
            if (_boxes[index + 1] is { } next)
            {
                next.Focus();
                next.CaretIndex = 0;
            }
            e.Handled = true;
        }
    }

    private void StepSegment(TextBox box, int index, int delta)
    {
        var spec = _specProvider(index);
        var max = spec.DynamicMaxOverride ?? spec.MaxValue;
        var min = spec.MinValue;
        int next;

        if (int.TryParse(box.Text, out var current))
        {
            next = current + delta;
            if (next > max)
                next = spec.WrapOnStep ? min : max;
            else if (next < min)
                next = spec.WrapOnStep ? max : min;
        }
        else
        {
            next = delta > 0 ? min : max;
        }

        var formatted = spec.PadWithZero
            ? next.ToString($"D{spec.MaxLength}")
            : next.ToString();

        _syncing = true;
        try
        {
            box.Text = formatted;
            box.SelectAll();
        }
        finally
        {
            _syncing = false;
        }

        _onCommitted();
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing || sender is not TextBox box || box.Tag is not int index)
            return;

        var raw = box.Text ?? string.Empty;
        if (raw.Any(c => _jumpChars.Contains(c)))
        {
            ApplyDelimitedText(raw, index);
            return;
        }

        var spec = _specProvider(index);
        var normalized = ClampDigits(raw, spec, enforceMin: false);
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

        _onCommitted();

        var last = LastVisibleSegmentIndex();
        if (normalized.Length >= spec.MaxLength && box.CaretIndex >= spec.MaxLength && index < last && box.IsFocused)
            FocusSegment(index + 1, selectAll: true);
    }

    private void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_syncing || sender is not TextBox box || box.Tag is not int index)
            return;

        var raw = box.Text ?? string.Empty;
        if (raw.Length > 0)
        {
            var spec = _specProvider(index);
            var clamped = ClampDigits(raw, spec, enforceMin: true);
            if (spec.PadWithZero && int.TryParse(clamped, out var num))
                clamped = num.ToString($"D{spec.MaxLength}");

            if (clamped != raw)
            {
                _syncing = true;
                try
                {
                    box.Text = clamped;
                }
                finally
                {
                    _syncing = false;
                }
            }
        }

        _onSegmentBlurred?.Invoke(index);
        _onCommitted();
    }

    private void ApplyDelimitedText(string input, int startIndex)
    {
        var splitChars = _jumpChars.ToArray();
        var parts = input.Split(splitChars);
        var last = LastVisibleSegmentIndex();

        _syncing = true;
        try
        {
            var targetStart = parts.Length > last ? 0 : startIndex;
            for (var p = 0; p < parts.Length && targetStart + p <= last; p++)
            {
                var seg = _boxes[targetStart + p];
                if (seg is null)
                    continue;

                var spec = _specProvider(targetStart + p);
                seg.Text = ClampDigits(parts[p], spec, enforceMin: false);
            }
        }
        finally
        {
            _syncing = false;
        }

        _onCommitted();

        var focusIdx = Math.Min(last, (parts.Length > last ? 0 : startIndex) + parts.Length - 1);
        FocusSegment(focusIdx, selectAll: false);
    }

    public static string ClampDigits(string input, ForgeSegmentSpec spec, bool enforceMin)
    {
        var digits = new string(input.Where(char.IsAsciiDigit).Take(spec.MaxLength).ToArray());
        if (digits.Length == 0)
            return string.Empty;

        var max = spec.DynamicMaxOverride ?? spec.MaxValue;
        if (int.TryParse(digits, out var val))
        {
            if (val > max)
                return spec.PadWithZero ? max.ToString($"D{spec.MaxLength}") : max.ToString();

            if ((enforceMin || digits.Length >= spec.MaxLength) && val < spec.MinValue)
                return spec.PadWithZero ? spec.MinValue.ToString($"D{spec.MaxLength}") : spec.MinValue.ToString();
        }

        return digits;
    }

    private void FocusSegment(int index, bool selectAll)
    {
        if (index < 0 || index >= _count || _boxes[index] is not { IsVisible: true } target)
            return;

        target.Focus();
        if (selectAll)
            target.SelectAll();
        else
            target.CaretIndex = target.Text?.Length ?? 0;
    }
}
