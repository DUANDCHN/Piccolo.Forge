using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 三段居中分格时间输入框（时 : 分 : 秒，24 小时制）：
/// 统一 TextBox 外框度量，支持 <c>ShowSeconds</c> 切换两段（时:分）/三段（时:分:秒），
/// 支持冒号跳格、↑/↓ 循环步进（0~23 / 0~59）及整串时间粘贴拆填。
/// </summary>
[TemplatePart("PART_Grid", typeof(Grid))]
[TemplatePart("PART_Part0", typeof(TextBox))]
[TemplatePart("PART_Part1", typeof(TextBox))]
[TemplatePart("PART_Part2", typeof(TextBox))]
[PseudoClasses(":error")]
public class ForgeTimeBox : TemplatedControl
{
    private const int SegmentCount = 3;

    /// <summary><see cref="SelectedTime"/> 的依赖属性（默认双向绑定并支持数据校验）。</summary>
    public static readonly StyledProperty<TimeSpan?> SelectedTimeProperty =
        AvaloniaProperty.Register<ForgeTimeBox, TimeSpan?>(
            nameof(SelectedTime),
            defaultBindingMode: BindingMode.TwoWay,
            enableDataValidation: true);

    /// <summary><see cref="Text"/> 的依赖属性（默认双向绑定并支持数据校验）。</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ForgeTimeBox, string?>(
            nameof(Text),
            defaultBindingMode: BindingMode.TwoWay,
            enableDataValidation: true);

    /// <summary><see cref="ShowSeconds"/> 的依赖属性，默认 <c>true</c>。</summary>
    public static readonly StyledProperty<bool> ShowSecondsProperty =
        AvaloniaProperty.Register<ForgeTimeBox, bool>(nameof(ShowSeconds), defaultValue: true);

    /// <summary><see cref="Separator"/> 的依赖属性，默认 <c>":"</c>。</summary>
    public static readonly StyledProperty<string> SeparatorProperty =
        AvaloniaProperty.Register<ForgeTimeBox, string>(nameof(Separator), defaultValue: ":");

    /// <summary><see cref="SegmentPlaceholder"/> 的依赖属性，默认 <c>"--"</c>。</summary>
    public static readonly StyledProperty<string?> SegmentPlaceholderProperty =
        AvaloniaProperty.Register<ForgeTimeBox, string?>(nameof(SegmentPlaceholder), defaultValue: "--");

    /// <summary><see cref="IsReadOnly"/> 的依赖属性。</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<ForgeTimeBox, bool>(nameof(IsReadOnly));

    private readonly ForgeSegmentGroup _segments;
    private Grid? _segmentsGrid;
    private bool _syncing;

    /// <summary>初始化时间分段输入框。</summary>
    public ForgeTimeBox()
    {
        _segments = new ForgeSegmentGroup(
            SegmentCount,
            static index => index switch
            {
                0 => new ForgeSegmentSpec(MaxLength: 2, MinValue: 0, MaxValue: 23, WrapOnStep: true, PadWithZero: true),
                _ => new ForgeSegmentSpec(MaxLength: 2, MinValue: 0, MaxValue: 59, WrapOnStep: true, PadWithZero: true),
            },
            [':', '.'],
            [Key.OemSemicolon, Key.OemPeriod, Key.Decimal],
            CommitFromSegments);
    }

    /// <summary>当前有效时间；必填段为空或未构成合法时间时为 <c>null</c>。</summary>
    public TimeSpan? SelectedTime
    {
        get => GetValue(SelectedTimeProperty);
        set => SetValue(SelectedTimeProperty, value);
    }

    /// <summary>格式化后的时间文本（如 <c>"14:30:00"</c> 或隐藏秒时的 <c>"14:30"</c>）；全部为空时为 <c>""</c>。</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>是否显示秒分段，默认 <c>true</c>；设为 <c>false</c> 时折叠秒格仅展示时:分。</summary>
    public bool ShowSeconds
    {
        get => GetValue(ShowSecondsProperty);
        set => SetValue(ShowSecondsProperty, value);
    }

    /// <summary>时分秒之间的分隔符，默认 <c>":"</c>。</summary>
    public string Separator
    {
        get => GetValue(SeparatorProperty);
        set => SetValue(SeparatorProperty, value);
    }

    /// <summary>各空分段内的居中占位提示，默认 <c>"--"</c>。</summary>
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

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        _segments.Detach();
        base.OnApplyTemplate(e);
        _segmentsGrid = e.NameScope.Find<Grid>("PART_Grid");
        _segments.Attach(e.NameScope, "PART_Part");
        UpdateSecondColumnWidth();

        if (SelectedTime is not null)
            SyncFromSelectedTime();
        else
            SyncFromText();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (_syncing)
            return;

        if (change.Property == ShowSecondsProperty)
        {
            UpdateSecondColumnWidth();
            CommitFromSegments();
        }
        else if (change.Property == SelectedTimeProperty)
        {
            SyncFromSelectedTime();
        }
        else if (change.Property == TextProperty)
        {
            SyncFromText();
        }
        else if (change.Property == SeparatorProperty)
        {
            CommitFromSegments();
        }
    }

    /// <inheritdoc />
    protected override void UpdateDataValidation(AvaloniaProperty property, BindingValueType state, Exception? error)
    {
        base.UpdateDataValidation(property, state, error);
        if (property == SelectedTimeProperty || property == TextProperty)
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

    private void UpdateSecondColumnWidth()
    {
        if (_segmentsGrid is { ColumnDefinitions.Count: 5 })
            _segmentsGrid.ColumnDefinitions[4].Width = ShowSeconds ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }

    private void SyncFromSelectedTime()
    {
        _syncing = true;
        try
        {
            if (SelectedTime is { } ts)
            {
                var h = ((ts.Hours % 24 + 24) % 24).ToString("D2");
                var m = ts.Minutes.ToString("D2");
                var s = ts.Seconds.ToString("D2");
                _segments.SetSegmentValues([h, m, s]);
                var sep = string.IsNullOrEmpty(Separator) ? ":" : Separator;
                var text = ShowSeconds ? string.Join(sep, h, m, s) : string.Join(sep, h, m);
                SetCurrentValue(TextProperty, text);
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
        var parts = string.IsNullOrWhiteSpace(raw) ? [] : raw.Split(':', '.');
        _segments.SetSegmentValues(parts);
        UpdateSelectedTimeFromSegments();
    }

    private void CommitFromSegments()
    {
        var activeCount = ShowSeconds ? SegmentCount : 2;
        var values = new string[activeCount];
        var any = false;
        for (var i = 0; i < activeCount; i++)
        {
            var t = _segments.GetRawText(i);
            values[i] = t;
            if (t.Length > 0)
                any = true;
        }

        var sep = string.IsNullOrEmpty(Separator) ? ":" : Separator;
        var nextText = any ? string.Join(sep, values) : string.Empty;

        _syncing = true;
        try
        {
            SetCurrentValue(TextProperty, nextText);
            UpdateSelectedTimeFromSegments();
        }
        finally
        {
            _syncing = false;
        }
    }

    private void UpdateSelectedTimeFromSegments()
    {
        var h = _segments.GetInt(0);
        var m = _segments.GetInt(1);
        var s = ShowSeconds ? _segments.GetInt(2) : 0;

        TimeSpan? nextTime = null;
        if (h is >= 0 and <= 23 && m is >= 0 and <= 59 && s is >= 0 and <= 59)
            nextTime = new TimeSpan(h.Value, m.Value, s.Value);

        var prevSync = _syncing;
        _syncing = true;
        try
        {
            SetCurrentValue(SelectedTimeProperty, nextTime);
        }
        finally
        {
            _syncing = prevSync;
        }
    }
}
