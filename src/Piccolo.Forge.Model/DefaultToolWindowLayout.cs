namespace Piccolo.Forge.Model;

/// <summary>
/// 上游的默认布局（新 UI / V2）。
/// </summary>
/// <remarks>
/// 对应 <c>platform/platform-impl/src/com/intellij/toolWindow/defaultToolWindowlayoutProvider.kt:254-288
/// · addPlatformDefaultsV2</c>。
/// <para>
/// order **不是**写死的数字：上游按 left → right → bottom 遍历，每根条带内部按加入顺序
/// 从 0 递增（<c>:210-218 · add</c>）。这里照抄这个生成方式。
/// </para>
/// </remarks>
public static class DefaultToolWindowLayout
{
    /// <summary>
    /// 造一份新 UI 的默认布局。
    /// </summary>
    /// <returns>默认布局。</returns>
    public static DesktopLayout CreateV2()
    {
        var builder = new List<WindowInfo>();
        Add(builder, ToolWindowAnchor.Left, LeftDefaults());
        Add(builder, ToolWindowAnchor.Right, RightDefaults());
        Add(builder, ToolWindowAnchor.Bottom, BottomDefaults());
        return new DesktopLayout(builder);
    }

    private static void Add(List<WindowInfo> result, ToolWindowAnchor anchor, IEnumerable<WindowInfo> stripe)
    {
        var order = 0;
        foreach (var info in stripe)
        {
            result.Add(info with { Anchor = anchor, Order = order++ });
        }
    }

    private static IEnumerable<WindowInfo> LeftDefaults() =>
    [
        new WindowInfo { Id = "Project", Weight = 0.25f, ContentUiType = ToolWindowContentUiType.Combo },
        new WindowInfo { Id = "Commit", Weight = 0.25f },
        new WindowInfo { Id = "Structure", Weight = 0.25f, IsSplit = true },
    ];

    private static IEnumerable<WindowInfo> RightDefaults() =>
    [
        new WindowInfo { Id = "Notifications", Weight = 0.25f, ContentUiType = ToolWindowContentUiType.Combo },
        new WindowInfo { Id = "AIAssistant", Weight = 0.25f },
        new WindowInfo { Id = "Database", Weight = 0.25f },
        new WindowInfo { Id = "Gradle", Weight = 0.25f },
        new WindowInfo { Id = "Maven", Weight = 0.25f },
    ];

    // 这五个没写 weight，取 builder 默认 WindowInfoImpl.DEFAULT_WEIGHT
    // （defaultToolWindowlayoutProvider.kt:308）
    private static IEnumerable<WindowInfo> BottomDefaults() =>
    [
        new WindowInfo { Id = "Version Control" },
        new WindowInfo { Id = "Problems" },
        new WindowInfo { Id = "Problems View" },
        new WindowInfo { Id = "Terminal" },
        new WindowInfo { Id = "Services" },
    ];
}
