using Avalonia;
using Avalonia.Controls;
using Piccolo.Forge.Model;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 浮动 / 独立窗口形态的宿主。
/// </summary>
/// <remarks>
/// 对应 <c>FloatingDecorator</c>（对话框，owner 是主窗口）与 <c>WindowedDecorator</c>（独立 frame），
/// 区别见 <c>工具窗口模型.md</c> §12.1。这里用 Avalonia 的 <see cref="Window"/> 表达两者，
/// 差别落在 <see cref="ShowInTaskbar"/>、标题和能否最大化上。
/// </remarks>
public sealed class ForgeFloatingToolWindow
{
    private readonly Window _window;

    /// <summary>
    /// 建一个外部形态的宿主窗口。
    /// </summary>
    /// <param name="type">FLOATING 或 WINDOWED。</param>
    /// <param name="toolWindowTitle">工具窗口的名字。</param>
    /// <param name="projectName">项目名，WINDOWED 的标题要带上它。</param>
    /// <param name="content">窗口里放什么。</param>
    /// <param name="host">
    /// 宿主自己的窗体（<see cref="ForgeToolWindowFrame.ExternalWindowFactory"/> 给的）；
    /// 不给就用库自带的朴素窗口。
    /// </param>
    /// <exception cref="ArgumentException">传了内部形态。</exception>
    public ForgeFloatingToolWindow(
        ToolWindowType type,
        string toolWindowTitle,
        string projectName,
        Control content,
        Window? host = null)
    {
        if (ToolWindowManager.IsInternal(type))
        {
            throw new ArgumentException("只有 FLOATING / WINDOWED 才有外部宿主窗口", nameof(type));
        }

        Type = type;

        if (host is not null)
        {
            // 宿主给了窗体（比如整套自绘 chrome 的那种）：只填标题和内容，
            // chrome 长什么样、有哪几个系统按钮，全归宿主 —— 库不越界改它。
            _window = host;
            _window.Title = ComposeTitle(type, toolWindowTitle, projectName);
            _window.Content = content;

            // ★ 但「在不在任务栏里」不是长相，是形态语义，得由库定：
            //   WINDOWED 是独立窗口（JFrame，任务栏里有自己的一项），
            //   FLOATING 是挂在主窗口上的浮动件（JDialog），不该在任务栏里另起一个
            //   （ToolWindowManagerDecorators.kt:81 / :98）。
            _window.ShowInTaskbar = type == ToolWindowType.Windowed;
            return;
        }

        _window = new Window
        {
            Title = ComposeTitle(type, toolWindowTitle, projectName),

            Content = content,

            // WINDOWED 是真正的独立窗口，FLOATING 是挂在主窗口上的对话框
            // （FloatingDecorator.java:63 是 JDialog，WindowedDecorator 是 JFrame）
            ShowInTaskbar = type == ToolWindowType.Windowed,
            CanResize = true,

            // ★ 两种形态差在**有没有系统标题栏**，这是上游的分法，不是我们的发挥：
            //   · WINDOWED = FrameWrapper 包的 JFrame（ToolWindowManagerDecorators.kt:98），
            //     普通带装饰的窗口 —— 最小化 / 最大化 / 关闭齐全，靠系统标题条拖。
            //   · FLOATING = JDialog，且 Windows 上 setUndecorated(true)
            //     （FloatingDecorator.java:102-108）—— 没有那层标题栏，
            //     面板自己的标题条（ForgeToolWindowHeader）就是它的把手。
            //   浮动取 BorderOnly：留边框、圆角、拖边改大小，只去掉标题栏那条
            //   （连带最小/最大/关闭也就没了，浮动本来一个都不该有）。
            //     · ExtendClientArea 不行 —— 它只是把标题栏"让"给内容画，
            //       那三个按钮仍然叠在右上角；
            //     · None 也不行 —— 边框、圆角、拖边改大小全没了，
            //       和基准（Rider 的浮动窗口，有细边框、圆角、能拉）对不上。
            //       上游 FloatingDecorator 是 undecorated + 自己塞四条 BorderItem 补回改大小，
            //       那是 Swing 时代的做法；Avalonia 这边 BorderOnly 就是同一件事，
            //       而且圆角、投影、贴边都由系统给，没必要自己画一套还画不全。
            WindowDecorations = type == ToolWindowType.Floating
                ? WindowDecorations.BorderOnly
                : WindowDecorations.Full,
        };
    }

    /// <summary>形态。</summary>
    public ToolWindowType Type { get; }

    /// <summary>
    /// 把面板内容摘出来还给调用方 —— 关窗之前必须做，否则宿主控件跟着窗口一起没了。
    /// </summary>
    public void ReleaseContent() => _window.Content = null;

    /// <summary>宿主窗口。</summary>
    public Window Window => _window;

    /// <summary>WINDOWED 才在任务栏里出现。</summary>
    public bool ShowInTaskbar => _window.ShowInTaskbar;

    /// <summary>
    /// 标题：FLOATING 只用工具窗口名，WINDOWED 还要带项目名。
    /// </summary>
    /// <remarks>
    /// 出处 <c>FloatingDecorator.java:92</c> 与
    /// <c>ToolWindowManagerDecorators.kt:103</c>（<c>"{stripeTitle} - {project.name}"</c>）。
    /// </remarks>
    /// <param name="type">形态。</param>
    /// <param name="toolWindowTitle">工具窗口的名字。</param>
    /// <param name="projectName">项目名。</param>
    /// <returns>窗口标题。</returns>
    public static string ComposeTitle(ToolWindowType type, string toolWindowTitle, string projectName)
        => type == ToolWindowType.Windowed ? $"{toolWindowTitle} - {projectName}" : toolWindowTitle;

    /// <summary>
    /// 按存档、屏幕和主窗口摆好位置。
    /// </summary>
    /// <remarks>三条分支照抄 <see cref="FloatingBoundsResolver.Resolve"/>（<c>工具窗口模型.md</c> §12.2）。</remarks>
    /// <param name="stored">存档里的 bounds。</param>
    /// <param name="screen">屏幕范围。</param>
    /// <param name="ideFrame">主窗口的 bounds，居中时用。</param>
    /// <param name="fallbackSize">没存过时用的尺寸。</param>
    /// <returns>最终用的 bounds。</returns>
    public FloatingBounds ApplyBounds(
        FloatingBounds? stored,
        FloatingBounds screen,
        FloatingBounds ideFrame,
        (int Width, int Height) fallbackSize)
    {
        var (bounds, needToCenter) = FloatingBoundsResolver.Resolve(stored, screen, fallbackSize);
        if (needToCenter)
        {
            bounds = FloatingBoundsResolver.CenterOn(bounds, ideFrame);
        }

        _window.Position = new PixelPoint(bounds.X, bounds.Y);
        _window.Width = bounds.Width;
        _window.Height = bounds.Height;

        return bounds;
    }

    /// <summary>
    /// 把当前窗口状态存回 <see cref="WindowInfo"/>。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ToolWindowManagerDecorators.kt:52-75 · saveFloatingOrWindowedState</c>：
    /// WINDOWED **最大化时要先退出最大化再取 bounds**，否则存下来的是屏幕尺寸。
    /// </remarks>
    /// <param name="info">要更新的窗口状态。</param>
    /// <returns>更新后的窗口状态。</returns>
    public WindowInfo SaveState(WindowInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        var maximized = Type == ToolWindowType.Windowed && _window.WindowState == WindowState.Maximized;
        if (maximized)
        {
            _window.WindowState = WindowState.Normal;
        }

        var bounds = new FloatingBounds(
            _window.Position.X,
            _window.Position.Y,
            (int)_window.Width,
            (int)_window.Height);

        return info with
        {
            FloatingBounds = bounds,
            IsMaximized = maximized,
            IsActiveOnStart = _window.IsActive,
        };
    }
}
