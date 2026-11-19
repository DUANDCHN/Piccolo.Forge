using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 三段居中分格日期输入框（年 / 月 / 日）：统一 TextBox 外框度量，
/// 默认采用斜杠分隔与 <c>YYYY / MM / DD</c> 占位，支持 <c>-</c>/<c>/</c> 跳段、
/// 按当月实际天数自动钳位、↑/↓ 步进与粘贴日期串自动拆填。
/// </summary>
[TemplatePart("PART_Part0", typeof(TextBox))]
[TemplatePart("PART_Part1", typeof(TextBox))]
[TemplatePart("PART_Part2", typeof(TextBox))]
[PseudoClasses(":error")]
public class ForgeDateBox : TemplatedControl
{
    private const int SegmentCount = 3;

    /// <summary><see cref="SelectedDate"/> 的依赖属性（默认双向绑定并支持数据校验）。</summary>
    public static readonly StyledProperty<DateTime?> SelectedDateProperty =
        AvaloniaProperty.Register<ForgeDateBox, DateTime?>(
            nameof(SelectedDate),
            defaultBindingMode: BindingMode.TwoWay,
            enableDataValidation: true);

    /// <summary><see cref="Text"/> 的依赖属性（默认双向绑定并支持数据校验）。</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ForgeDateBox, string?>(
            nameof(Text),
            defaultBindingMode: BindingMode.TwoWay,
            enableDataValidation: true);

    /// <summary><see cref="Separator"/> 的依赖属性，默认 <c>"/"</c>。</summary>
    public static readonly StyledProperty<string> SeparatorProperty =
        AvaloniaProperty.Register<ForgeDateBox, string>(nameof(Separator), defaultValue: "/");

    /// <summary><see cref="YearPlaceholder"/> 的依赖属性，默认 <c>"YYYY"</c>。</summary>
    public static readonly StyledProperty<string?> YearPlaceholderProperty =
        AvaloniaProperty.Register<ForgeDateBox, string?>(nameof(YearPlaceholder), defaultValue: "YYYY");

    /// <summary><see cref="MonthPlaceholder"/> 的依赖属性，默认 <c>"MM"</c>。</summary>
    public static readonly StyledProperty<string?> MonthPlaceholderProperty =
        AvaloniaProperty.Register<ForgeDateBox, string?>(nameof(MonthPlaceholder), defaultValue: "MM");

    /// <summary><see cref="DayPlaceholder"/> 的依赖属性，默认 <c>"DD"</c>。</summary>
    public static readonly StyledProperty<string?> DayPlaceholderProperty =
        AvaloniaProperty.Register<ForgeDateBox, string?>(nameof(DayPlaceholder), defaultValue: "DD");

    /// <summary><see cref="IsReadOnly"/> 的依赖属性。</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<ForgeDateBox, bool>(nameof(IsReadOnly));

    private readonly ForgeSegmentGroup _segments;
    private bool _syncing;

    /// <summary>初始化日期分段输入框。</summary>
    public ForgeDateBox()
    {
        _segments = new ForgeSegmentGroup(
            SegmentCount,
            GetSegmentSpec,
            ['/', '-', '.'],
            [Key.Oem2, Key.Divide, Key.OemMinus, Key.Subtract, Key.OemPeriod, Key.Decimal],
            CommitFromSegments,
            OnSegmentBlurred);
    }

    /// <summary>当前有效日期；任意段为空或未构成合法日期时为 <c>null</c>。</summary>
    public DateTime? SelectedDate
    {
        get => GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    /// <summary>分隔后的日期字符串（如 <c>"2026/11/19"</c>）；全部为空时为 <c>""</c>。</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>年月日之间的分隔符，默认 <c>"/"</c>。</summary>
    public string Separator
    {
        get => GetValue(SeparatorProperty);
        set => SetValue(SeparatorProperty, value);
    }

    /// <summary>年份空占位提示，默认 <c>"YYYY"</c>。</summary>
    public string? YearPlaceholder
    {
        get => GetValue(YearPlaceholderProperty);
        set => SetValue(YearPlaceholderProperty, value);
    }

    /// <summary>月份空占位提示，默认 <c>"MM"</c>。</summary>
    public string? MonthPlaceholder
    {
        get => GetValue(MonthPlaceholderProperty);
        set => SetValue(MonthPlaceholderProperty, value);
    }

    /// <summary>日期空占位提示，默认 <c>"DD"</c>。</summary>
    public string? DayPlaceholder
    {
        get => GetValue(DayPlaceholderProperty);
        set => SetValue(DayPlaceholderProperty, value);
    }

    /// <summary>是否只读。</summary>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        _segments.Detach();
        base.OnApplyTemplate(e);
        _segments.Attach(e.NameScope, "PART_Part");

        if (SelectedDate is not null)
            SyncFromSelectedDate();
        else
            SyncFromText();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (_syncing)
            return;

        if (change.Property == SelectedDateProperty)
            SyncFromSelectedDate();
        else if (change.Property == TextProperty)
            SyncFromText();
        else if (change.Property == SeparatorProperty)
            CommitFromSegments();
    }

    /// <inheritdoc />
    protected override void UpdateDataValidation(AvaloniaProperty property, BindingValueType state, Exception? error)
    {
        base.UpdateDataValidation(property, state, error);
        if (property == SelectedDateProperty || property == TextProperty)
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

    private ForgeSegmentSpec GetSegmentSpec(int index) => index switch
    {
        0 => new ForgeSegmentSpec(MaxLength: 4, MinValue: 1, MaxValue: 9999, WrapOnStep: false, PadWithZero: false),
        1 => new ForgeSegmentSpec(MaxLength: 2, MinValue: 1, MaxValue: 12, WrapOnStep: true, PadWithZero: true),
        2 => new ForgeSegmentSpec(MaxLength: 2, MinValue: 1, MaxValue: MaxDaysForCurrentYearMonth(), WrapOnStep: true, PadWithZero: true),
        _ => new ForgeSegmentSpec(MaxLength: 2, MinValue: 1, MaxValue: 31, WrapOnStep: true, PadWithZero: true),
    };

    private int MaxDaysForCurrentYearMonth()
    {
        var month = _segments.GetInt(1);
        if (month is null or < 1 or > 12)
            return 31;

        var year = _segments.GetInt(0);
        // 年份未完整输入时仍允许按闰年（2024）检验 2 月 29 日，待年份失焦再做严格闰年收口。
        var effectiveYear = year is >= 1 and <= 9999 ? year.Value : 2024;
        return DateTime.DaysInMonth(effectiveYear, month.Value);
    }

    private void OnSegmentBlurred(int index)
    {
        // 年/月变化后同步把超出当月天数的日段收口（例如 1/31 切到 2 月 -> 28 或 29）。
        if (index is 0 or 1)
            ClampDayToMonth();
    }

    private void ClampDayToMonth()
    {
        var day = _segments.GetInt(2);
        if (day is null)
            return;

        var maxDays = MaxDaysForCurrentYearMonth();
        if (day.Value > maxDays)
            _segments.SetSegmentText(2, maxDays.ToString("D2"));
    }

    private void SyncFromSelectedDate()
    {
        _syncing = true;
        try
        {
            if (SelectedDate is { } dt)
            {
                var y = dt.Year.ToString("D4");
                var m = dt.Month.ToString("D2");
                var d = dt.Day.ToString("D2");
                _segments.SetSegmentValues([y, m, d]);
                var sep = string.IsNullOrEmpty(Separator) ? "/" : Separator;
                SetCurrentValue(TextProperty, string.Join(sep, y, m, d));
            }
            else
            {
                _segments.SetSegmentValues([]);
                SetCurrentValue(TextProperty, string.Empty);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SyncFromText()
    {
        var raw = Text;
        var parts = string.IsNullOrWhiteSpace(raw) ? [] : raw.Split('/', '-', '.');
        _segments.SetSegmentValues(parts);
        ClampDayToMonth();
        UpdateSelectedDateFromSegments();
    }

    private void CommitFromSegments()
    {
        // 年/月变化即时检查日上限，避免 2026/02/31 这种非法日期留存。
        ClampDayToMonth();

        var values = new string[SegmentCount];
        var any = false;
        for (var i = 0; i < SegmentCount; i++)
        {
            var t = _segments.GetRawText(i);
            values[i] = t;
            if (t.Length > 0)
                any = true;
        }

        var sep = string.IsNullOrEmpty(Separator) ? "/" : Separator;
        var nextText = any ? string.Join(sep, values) : string.Empty;

        _syncing = true;
        try
        {
            SetCurrentValue(TextProperty, nextText);
            UpdateSelectedDateFromSegments();
        }
        finally
        {
            _syncing = false;
        }
    }

    private void UpdateSelectedDateFromSegments()
    {
        var y = _segments.GetInt(0);
        var m = _segments.GetInt(1);
        var d = _segments.GetInt(2);

        DateTime? nextDate = null;
        if (y is >= 1 and <= 9999 && m is >= 1 and <= 12 && d is >= 1)
        {
            var maxDays = DateTime.DaysInMonth(y.Value, m.Value);
            if (d.Value <= maxDays)
                nextDate = new DateTime(y.Value, m.Value, d.Value);
        }

        var prevSync = _syncing;
        _syncing = true;
        try
        {
            SetCurrentValue(SelectedDateProperty, nextDate);
        }
        finally
        {
            _syncing = prevSync;
        }
    }
}
