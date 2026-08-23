using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Threading;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 通知宿主：与 <see cref="WindowNotificationManager"/> 完全一样，只多一件事 ——
/// **鼠标停在通知上时不自动消隐**，移开后按剩余时间续（不重置满）。
/// </summary>
/// <remarks>
/// <para>
/// 为什么要专门做一个：框架那套的自动消隐是 <c>DispatcherTimer.RunOnce(card.Close, expiration)</c>
/// 一句话，**没有任何悬停暂停**，而且计时句柄留在方法内部、外面拿不到。
/// 于是读一条带详情或带动作按钮的通知时，读到一半它就自己走了。
/// </para>
/// <para>
/// 做法：调 base 时把 expiration 传 <see cref="TimeSpan.Zero"/> —— 框架看到 Zero 就**不启**定时器
/// （见其 Show 里的提前返回），布局、定位、进出场动画、MaxItems 这些照旧由它管；
/// 计时改由本类按卡片各自管一份，可暂停可续。**只接管计时，不重写显示。**
/// </para>
/// <para>
/// ⚠ <c>WindowNotificationManager.Show</c> 不是虚方法，所以这里的同名重载是**隐藏**不是重写：
/// 宿主必须以 <see cref="ForgeNotificationManager"/> 类型持有它，才走得到带暂停的那份。
/// 用接口（<c>INotificationManager</c>）持有会落回框架实现，暂停就没了。
/// </para>
/// </remarks>
[ForgeThemeless("只改计时行为、不改长相：StyleKeyOverride 指回 WindowNotificationManager，" +
                "用的就是框架那份主题。给它单独加一份 ControlTheme 反而会与基类那份分叉。")]
public class ForgeNotificationManager : WindowNotificationManager
{
    private readonly Dictionary<NotificationCard, PendingClose> _pending = [];
    private TimeSpan _nextExpiration = DefaultExpiration;
    private Panel? _host;

    /// <summary>不指定时长时用多久。与框架的默认值一致（5 秒）。</summary>
    public static readonly TimeSpan DefaultExpiration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 用基类的样式键 —— 本类只改计时行为，长相与框架的完全一样，没有自己的模板。
    /// </summary>
    /// <remarks>
    /// ⚠ 不写这一条的后果是**静默不画**：Avalonia 按控件的 StyleKey 找隐式 ControlTheme，
    /// 默认取实际类型，于是 <c>ForgeNotificationManager</c> 找不到任何主题 → 没有模板 →
    /// 拿不到 <c>PART_Items</c> → 通知加进去了却一张也不显示，且不报错。
    /// </remarks>
    protected override Type StyleKeyOverride => typeof(WindowNotificationManager);

    /// <inheritdoc />
    protected override void OnApplyTemplate(global::Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // 挂在**容器**上而不是 Show 的返回值上：Show 不返回卡片，而容器的子元素增删
        // 是所有显示路径（含 INotification 那两个重载）都会经过的唯一入口。
        if (_host?.Children is INotifyCollectionChanged old)
        {
            old.CollectionChanged -= OnCardsChanged;
        }

        _host = e.NameScope.Find<Panel>("PART_Items");

        if (_host?.Children is INotifyCollectionChanged now)
        {
            now.CollectionChanged += OnCardsChanged;
        }
    }

    /// <summary>
    /// 显示一条通知，鼠标停在上面时暂停消隐。参数含义与框架
    /// <c>WindowNotificationManager.Show</c> 的同名重载相同。
    /// </summary>
    /// <param name="content">卡片内容。传自己的视图模型即可，由 DataTemplate 渲染。</param>
    /// <param name="type">严重度，决定卡片左侧那颗图标（主题按 <c>:information</c> 等伪类切换）。</param>
    /// <param name="expiration">多久后自动消隐；<see cref="TimeSpan.Zero"/> 表示常驻，<c>null</c> 用默认 5 秒。</param>
    /// <param name="onClick">点卡片时。</param>
    /// <param name="onClose">卡片消失时（自动消隐与手动关闭都会调）。</param>
    /// <param name="classes">附加到卡片上的样式类。</param>
    public new void Show(
        object content,
        NotificationType type = NotificationType.Information,
        TimeSpan? expiration = null,
        Action? onClick = null,
        Action? onClose = null,
        string[]? classes = null)
    {
        // 供 OnCardsChanged 取用：base.Show 是同步的，卡片在它内部就被加进容器，
        // 所以这个字段在回调里读到的必定是本次的值。
        _nextExpiration = expiration ?? DefaultExpiration;

        // ★ Zero = 让框架别启它那个不可暂停的定时器。计时由本类接管。
        base.Show(content, type, TimeSpan.Zero, onClick, onClose, classes);
    }

    private void OnCardsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var item in e.NewItems ?? System.Array.Empty<object>())
        {
            if (item is NotificationCard card)
            {
                Attach(card, _nextExpiration);
            }
        }

        foreach (var item in e.OldItems ?? System.Array.Empty<object>())
        {
            if (item is NotificationCard card)
            {
                Detach(card);
            }
        }
    }

    private void Attach(NotificationCard card, TimeSpan expiration)
    {
        if (expiration <= TimeSpan.Zero)
        {
            return;   // 常驻，只能手动关
        }

        var pending = new PendingClose(card, expiration);
        _pending[card] = pending;

        card.PointerEntered += OnCardPointerEntered;
        card.PointerExited += OnCardPointerExited;
        pending.Resume();
    }

    private void Detach(NotificationCard card)
    {
        if (!_pending.Remove(card, out var pending))
        {
            return;
        }

        card.PointerEntered -= OnCardPointerEntered;
        card.PointerExited -= OnCardPointerExited;
        pending.Dispose();
    }

    private void OnCardPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is NotificationCard card && _pending.TryGetValue(card, out var pending))
        {
            pending.Pause();
        }
    }

    private void OnCardPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is NotificationCard card && _pending.TryGetValue(card, out var pending))
        {
            pending.Resume();
        }
    }

    /// <summary>一张卡片的待关闭计时：可暂停，续的是**剩余**时间而不是重新计满。</summary>
    private sealed class PendingClose(NotificationCard card, TimeSpan expiration) : IDisposable
    {
        private readonly DispatcherTimer _timer = new();
        private readonly System.Diagnostics.Stopwatch _elapsed = new();
        private TimeSpan _remaining = expiration;

        public void Resume()
        {
            if (_remaining <= TimeSpan.Zero)
            {
                return;
            }

            _timer.Interval = _remaining;
            _timer.Tick -= OnTick;
            _timer.Tick += OnTick;
            _elapsed.Restart();
            _timer.Start();
        }

        public void Pause()
        {
            if (!_timer.IsEnabled)
            {
                return;
            }

            _timer.Stop();
            _elapsed.Stop();
            _remaining -= _elapsed.Elapsed;
        }

        private void OnTick(object? sender, EventArgs e)
        {
            _timer.Stop();
            card.Close();
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
        }
    }
}
