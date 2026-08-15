namespace Piccolo.Forge.Themes;

/// <summary>
/// 上游的度量常量，**未缩放值**（unscaled），与上游源码写法一致。
/// </summary>
/// <remarks>
/// 出处逐条见 <c>docs/upstream/度量与主题键.md</c>。
/// 上游每个度量都是「代码兜底常量 + 主题键可覆盖」两层
/// （<c>platform/util/ui/src/com/intellij/util/ui/JBUI.java:1170-1180</c> 是典型形状），
/// 这里放的是兜底常量，键名见 <see cref="ForgeThemeKeys"/>。
/// </remarks>
public static class ForgeMetrics
{
    /// <summary>条带按钮尺寸 40×40（<c>JBUI.java:1322-1324 · defaultStripeToolbarButtonSize</c>）。</summary>
    public const double StripeButtonSize = 40;

    /// <summary>条带按钮图标尺寸 20（<c>JBUI.java:1334-1336</c>）。</summary>
    public const double StripeButtonIconSize = 20;

    /// <summary>条带按钮图标四边 padding 5（<c>JBUI.java:1346-1348</c>）。</summary>
    public const double StripeButtonIconPadding = 5;

    /// <summary>条带按钮圆角 12（紧凑模式 8，<c>JBUI.java:1358-1360</c>）。</summary>
    public const double StripeButtonArc = 12;

    /// <summary>条带按钮圆角（紧凑模式）8。</summary>
    public const double StripeButtonArcCompact = 8;

    /// <summary>条带内按钮之间的间隙 0（<c>ToolWindowToolbar.kt:196</c>，<c>VerticalFlowLayout(0, 0)</c>）。</summary>
    public const double StripeButtonGap = 0;

    /// <summary>split 分组分隔符的占位尺寸 32×11（<c>StripeButtonSeparator.kt:22-24</c>）。</summary>
    public const double StripeSeparatorWidth = 32;

    /// <summary>split 分组分隔符的占位高度（<c>StripeButtonSeparator.kt:22-24</c>）。</summary>
    public const double StripeSeparatorHeight = 11;

    /// <summary>分隔符实际画出来的线宽 24（<c>StripeButtonSeparator.kt:31</c>）。</summary>
    public const double StripeSeparatorLineWidth = 24;

    /// <summary>分隔符实际画出来的线高 1（<c>StripeButtonSeparator.kt:32</c>）。</summary>
    public const double StripeSeparatorLineHeight = 1;

    /// <summary>开「显示名字」后条带的默认宽度 59（<c>ResizeStripeManager.kt:215-218</c>）。</summary>
    public const double StripeNamedDefaultWidth = 59;

    /// <summary>条带可拖动的最小宽度 40（紧凑模式 33，<c>ResizeStripeManager.kt:138-141</c>）。</summary>
    public const double StripeMinWidth = 40;

    /// <summary>条带可拖动的最小宽度（紧凑模式）33。</summary>
    public const double StripeMinWidthCompact = 33;

    /// <summary>条带可拖动的最大宽度 100（<c>ResizeStripeManager.kt:143-146</c>）。</summary>
    public const double StripeMaxWidth = 100;

    /// <summary>工具窗口标题条高度 41（<c>JBUI.java:1178-1180 · defaultHeaderHeight</c>）。</summary>
    public const double HeaderHeight = 41;

    /// <summary>标题条标题文字的左内边距 12（<c>JBUI.java:1182-1184</c>）。</summary>
    public const double HeaderLabelInsetLeft = 12;

    /// <summary>标题条标题文字的右内边距 16（<c>JBUI.java:1182-1184</c>）。</summary>
    public const double HeaderLabelInsetRight = 16;

    /// <summary>标题条工具栏的左内边距 12（<c>JBUI.java:1186-1188</c>）。</summary>
    public const double HeaderToolbarInsetLeft = 12;

    /// <summary>标题条工具栏的右内边距 8（<c>JBUI.java:1186-1188</c>）。</summary>
    public const double HeaderToolbarInsetRight = 8;

    /// <summary>标题条标签页左右内边距 12（<c>JBUI.java:1133-1135</c>）。</summary>
    public const double HeaderTabInset = 12;

    /// <summary>选中标签页下划线高度 3（<c>JBUI.java:593 · DefaultTabs.UNDERLINE_HEIGHT</c>）。</summary>
    public const double HeaderTabUnderlineHeight = 3;

    /// <summary>选中标签页下划线圆角 4（<c>JBUI.java:1129-1131</c>）。</summary>
    public const double HeaderTabUnderlineArc = 4;

    /// <summary>主分隔器的可见宽度 0（<c>ToolWindowPane.kt:197</c>）。</summary>
    public const double SplitterDividerWidth = 0;

    /// <summary>分隔器的鼠标热区 6（Registry <c>ide.splitter.mouseZone</c>，<c>registry.properties:290</c>）。</summary>
    public const double SplitterMouseZone = 6;

    /// <summary>编辑区与工具窗口的最小尺寸 30（Registry <c>ide.mainSplitter.min.size</c>，<c>registry.properties:160</c>）。</summary>
    public const double MainSplitterMinSize = 30;

    /// <summary>标题条顶部保留给「拖动改大小」的高度 13（Registry <c>ide.new.tool.window.resize.area.height</c>，<c>registry.properties:130</c>）。</summary>
    public const double HeaderResizeAreaHeight = 13;

    /// <summary>同一格里两个窗口之间的分隔器宽度 1（<c>ToolWindowPane.kt:617 · MySplitter</c> 是 OnePixelSplitter）。</summary>
    public const double SplitDividerWidth = 1;

    /// <summary>
    /// 权重规范化：<c>&lt;= 0</c> 取默认 0.33，<c>&gt;= 1</c> 取 <c>1 - 0.33</c>，否则原样。
    /// </summary>
    /// <remarks>对应 <c>ToolWindowPane.kt:85-90 · normalizeWeight</c>。所有下发到分隔器的权重都过这一道。</remarks>
    /// <param name="weight">原始权重。</param>
    /// <returns>规范化后的权重。</returns>
    public static float NormalizeWeight(float weight)
    {
        const float defaultWeight = 0.33f;
        if (weight <= 0)
        {
            return defaultWeight;
        }

        return weight >= 1 ? 1 - defaultWeight : weight;
    }
}
