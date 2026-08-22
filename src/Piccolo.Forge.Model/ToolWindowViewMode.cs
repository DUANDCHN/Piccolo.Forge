namespace Piccolo.Forge.Model;

/// <summary>
/// 「视图模式」菜单里的一项。
/// </summary>
/// <remarks>
/// ⚠ 它**不等于** <see cref="ToolWindowType"/>：上游的视图模式是
/// <c>type × isAutoHide</c> 两个字段的组合，判据在
/// <c>ToolWindowManagerLifecycle.kt:116</c>：
/// <code>if (!(windowInfo.isAutoHide || windowInfo.type == ToolWindowType.SLIDING)) return</code>
/// 也就是**失焦自动收起 = isAutoHide 或 SLIDING**。所以「停靠不固定」不是一种 type，
/// 而是"停靠着、但失焦就收起"；把 SLIDING 摆到那一格是把「取消停靠」错认成了它。
/// </remarks>
public enum ToolWindowViewMode
{
    /// <summary>停靠固定：占住那条边，一直在（<c>DOCKED</c> + 不自动收起）。</summary>
    DockPinned,

    /// <summary>停靠不固定：一样占位，但焦点一走就收起（<c>DOCKED</c> + <c>isAutoHide</c>）。</summary>
    DockUnpinned,

    /// <summary>取消停靠：滑出来盖在编辑区上、不挤占它，焦点一走也收起（<c>SLIDING</c>）。</summary>
    Undock,

    /// <summary>浮动：挂在主窗口上的小窗（<c>FLOATING</c>）。</summary>
    Float,

    /// <summary>窗口：真正的独立窗口（<c>WINDOWED</c>）。</summary>
    Window,
}

/// <summary>视图模式与 <see cref="WindowInfo"/> 之间的换算。</summary>
public static class ToolWindowViewModes
{
    /// <summary>这个窗口现在处于哪种视图模式。</summary>
    /// <param name="info">窗口状态。</param>
    /// <returns>视图模式。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="info"/> 为 <see langword="null"/>。</exception>
    public static ToolWindowViewMode Resolve(WindowInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        return info.Type switch
        {
            ToolWindowType.Sliding => ToolWindowViewMode.Undock,
            ToolWindowType.Floating => ToolWindowViewMode.Float,
            ToolWindowType.Windowed => ToolWindowViewMode.Window,
            _ => info.IsAutoHide ? ToolWindowViewMode.DockUnpinned : ToolWindowViewMode.DockPinned,
        };
    }

    /// <summary>这种视图模式对应哪种 <see cref="ToolWindowType"/>。</summary>
    /// <param name="mode">视图模式。</param>
    /// <returns>形态。</returns>
    public static ToolWindowType TypeOf(ToolWindowViewMode mode) => mode switch
    {
        ToolWindowViewMode.Undock => ToolWindowType.Sliding,
        ToolWindowViewMode.Float => ToolWindowType.Floating,
        ToolWindowViewMode.Window => ToolWindowType.Windowed,
        _ => ToolWindowType.Docked,
    };

    /// <summary>这种视图模式下是否失焦自动收起。</summary>
    /// <remarks>SLIDING 恒收起（判据里的另一半），所以它的 <c>isAutoHide</c> 取值无所谓，置 <see langword="false"/>。</remarks>
    /// <param name="mode">视图模式。</param>
    /// <returns>是否自动收起。</returns>
    public static bool AutoHideOf(ToolWindowViewMode mode) => mode == ToolWindowViewMode.DockUnpinned;

    /// <summary>
    /// 切到某种视图模式。
    /// </summary>
    /// <remarks>两个字段都要落：先 <c>isAutoHide</c> 再 <c>type</c>，
    /// 因为切 type 可能带出显示/隐藏，收起判据要用到已经更新过的 <c>isAutoHide</c>。</remarks>
    /// <param name="manager">管理器。</param>
    /// <param name="id">工具窗口 id。</param>
    /// <param name="mode">目标视图模式。</param>
    /// <exception cref="ArgumentNullException"><paramref name="manager"/> 为 <see langword="null"/>。</exception>
    public static void Apply(ToolWindowManager manager, string id, ToolWindowViewMode mode)
    {
        ArgumentNullException.ThrowIfNull(manager);

        manager.SetAutoHide(id, AutoHideOf(mode));
        manager.SetType(id, TypeOf(mode));
    }
}
