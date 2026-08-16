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
    /// <remarks>
    /// ⚠ <b>画界面别用这三个常量，用 <see cref="ForgeStripeButtonMetrics.ForTheme"/>。</b>
    /// 这里是**代码兜底**（主题一个键都没写时的值），而 Islands 主题把尺寸改成了 37×40、
    /// 内边距改成了左右镜像不对称。直接用兜底值＝把 Islands 画成 expUI 的样子。
    /// </remarks>
    public const double StripeButtonSize = 40;

    /// <summary>条带按钮图标尺寸 20（<c>JBUI.java:1334-1336</c>）。</summary>
    /// <remarks>兜底值，取用见 <see cref="StripeButtonSize"/> 的说明。</remarks>
    public const double StripeButtonIconSize = 20;

    /// <summary>条带按钮图标四边 padding 5（<c>JBUI.java:1346-1348</c>）。</summary>
    /// <remarks>兜底值，取用见 <see cref="StripeButtonSize"/> 的说明。</remarks>
    public const double StripeButtonIconPadding = 5;

    /// <summary>
    /// 条带按钮圆角 12（紧凑模式 8，<c>JBUI.java:1358-1360 · stripeButtonArc</c>）。
    /// </summary>
    /// <remarks>
    /// ⚠ <b>这是直径，半径是它的一半。</b>上游最终画在
    /// <c>IdeaActionButtonLook.java:36-37</c>：
    /// <code>
    /// float arc = getButtonArc().getFloat();
    /// g2.fill(new RoundRectangle2D.Float(x, y, w, h, arc, arc));
    /// </code>
    /// Java2D 的 <c>RoundRectangle2D(x, y, w, h, arcw, arch)</c> 里 <c>arcw/arch</c> 是
    /// **圆角外接盒的宽高**，即直径。<c>Island.arc</c> 是同一套语义。
    /// <para>
    /// Avalonia 的 <c>RoundedRect(rect, radius)</c> 收的是**半径**，直接把这个值传进去
    /// 会得到大一倍的圆角。取用一律走 <c>ForgeSquareButtonBase.CornerRadius</c>。
    /// </para>
    /// </remarks>
    public const double StripeButtonArc = 12;

    /// <summary>条带按钮圆角（紧凑模式）8。**直径**，说明见 <see cref="StripeButtonArc"/>。</summary>
    public const double StripeButtonArcCompact = 8;

    /// <summary>
    /// 拖到「另一半」时那个虚线方框的边长 <b>30</b>
    /// （<c>AbstractDroppableStripe.kt:645 · val size = JBUI.scale(30)</c>）。
    /// </summary>
    public const double StripeSplitDropBoxSize = 30;

    /// <summary>
    /// 目标停靠区高亮的兜底尺寸 <b>350</b> —— 那条边一个窗口都没开着时用
    /// （<c>ToolWindowToolbar.kt:271 · return JBUI.scale(350)</c>）。
    /// </summary>
    public const double DropAreaFallbackSize = 350;

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

    /// <summary>选中标签页下划线圆角 4（<c>JBUI.java:1129-1131</c>）。**直径**，同 <see cref="StripeButtonArc"/>。</summary>
    public const double HeaderTabUnderlineArc = 4;

    /// <summary>tab 起始的横向偏移 4（<c>TabContentLayout.java:59 · TAB_LAYOUT_START</c>）。</summary>
    public const double TabLayoutStart = 4;

    /// <summary>单个 tab 的最大宽度 400（<c>ContentTabLabel.java:48 · MAX_WIDTH</c>）。</summary>
    public const double TabMaxWidth = 400;

    /// <summary>「更多」图标的边距 6（<c>TabContentLayout.java:58 · MORE_ICON_BORDER</c>）。</summary>
    public const double TabMoreIconBorder = 6;

    /// <summary>combo 布局里 id 与下拉之间的间距 3（<c>ComboContentLayout.java:57</c>）。</summary>
    public const double ComboIdGap = 3;

    /// <summary>选中 tab 的下划线相对底边下移的厚度 1（<c>ToolWindowTabPainter.kt:35-36</c>）。</summary>
    public const double TabBorderThickness = 1;

    /// <summary>tab 上小图标之间的间距 3（<c>ContentLabel.java:46 · ICONS_GAP</c>）。</summary>
    public const double TabIconsGap = 3;

    /// <summary>tab 右侧图标区距右缘的偏移 4（<c>ContentLabel.java:252-257</c>）。</summary>
    public const double TabRightIconInset = 4;

    /// <summary>「更多」弹窗的最小宽度 300（<c>MoreSquareStripeButton.kt:106</c>）。</summary>
    public const double MorePopupMinWidth = 300;

    /// <summary>浮动窗口淡出的帧间隔 15ms（<c>FloatingDecorator.java:73 · DELAY</c>）。</summary>
    public const int FloatingFadeFrameDelayMs = 15;

    /// <summary>浮动窗口淡出的总帧数 7（<c>FloatingDecorator.java:74 · TOTAL_FRAME_COUNT</c>）。</summary>
    public const int FloatingFadeTotalFrames = 7;

    /// <summary>变透明前的等待 1500ms（<c>UISettingsState.kt:182 · alphaModeDelay</c>）。</summary>
    public const int FloatingFadeDelayMs = 1500;

    /// <summary>失活时的目标透明度 0.5（<c>UISettingsState.kt:184 · alphaModeRatio</c>）。</summary>
    public const float FloatingFadeRatio = 0.5f;

    /// <summary>浮动窗口淡出默认**关闭**（<c>UISettingsState.kt:180 · enableAlphaMode</c>）。</summary>
    public const bool FloatingFadeEnabledByDefault = false;

    /// <summary>
    /// 编辑区标签页高度的**基数** 28（<c>SingleHeightTabs.kt:15 · UNSCALED_PREF_HEIGHT</c>）。
    /// </summary>
    /// <remarks>
    /// ⚠ <b>这不是标签页的实际高度。</b>真正的行高在
    /// <c>EditorTabbedContainer.kt:784-788 · EditorTabLabel.getPreferredHeight</c>：
    /// <code>
    /// height = UNSCALED_PREF_HEIGHT - tabInsets.top - tabInsets.bottom
    /// </code>
    /// 而 <c>EditorTabs.tabInsets</c> 的**上下是负值**，所以是往上加：
    /// expUI <c>insets(-7, 8)</c> → 28+7+7 = <b>42</b>；Islands <c>-6,8,-6,8</c> → 28+6+6 = <b>40</b>。
    /// 取实际行高一律用 <see cref="EditorTabRowHeight"/>。
    /// <para>
    /// 早先直接把 28 当行高用，于是 Islands 那个选中块只剩 20 高
    /// （<c>vOffset = max(28-28, 8) = 8</c>），实机对比明显偏矮。
    /// </para>
    /// <para><c>JBUI.java:644</c> 那个 35 是 <c>DebuggerTabs</c> 的，别抄错。</para>
    /// </remarks>
    public const double EditorTabHeight = 28;

    /// <summary>编辑区标签页上下内边距：expUI <b>-7</b>（<c>JBUI.java:682-684</c>）。</summary>
    /// <remarks>负值是真的，行高靠它加出来，见 <see cref="EditorTabHeight"/>。</remarks>
    public const double EditorTabInsetVerticalExpUi = -7;

    /// <summary>编辑区标签页上下内边距：Islands <b>-6</b>（<c>ManyIslands*.theme.json</c> 的 <c>tabInsets</c>）。</summary>
    public const double EditorTabInsetVerticalIslands = -6;

    /// <summary>按族算出编辑区标签页的**实际行高**。</summary>
    /// <param name="islands">是不是 Islands 族。</param>
    /// <returns>行高：Islands 40、expUI 42。</returns>
    public static double EditorTabRowHeight(bool islands)
    {
        var inset = islands ? EditorTabInsetVerticalIslands : EditorTabInsetVerticalExpUi;
        return EditorTabHeight - inset - inset;
    }

    /// <summary>编辑区标签页的左右内边距 8（<c>JBUI.java:682-684 · EditorTabs.tabInsets</c>，新 UI）。</summary>
    /// <remarks>
    /// ⚠ <b>这只是外面那一层。</b>标签页有**两层**内边距（<c>TabLabel.kt:583-587</c>）：
    /// <code>
    /// border               = EmptyBorder(labelInsets)     ← 就是这个 8
    /// labelPlaceholder.border = EmptyBorder(contentInsets) ← 还有一层
    /// </code>
    /// 文字到标签页左缘的实际距离是**两层之和**，见 <see cref="EditorTabContentInsetLeft"/>。
    /// 早先只算了外层，于是左右看着都偏窄。
    /// </remarks>
    public const double EditorTabInsetHorizontal = 8;

    /// <summary>
    /// 编辑区标签页**内层**左内边距 4（<c>EditorTabs.tabContentActionsRightInsets</c>，
    /// 代码兜底 <c>insets(0, 4, 0, 6)</c>，<c>JBUI.java:698-708</c>）。
    /// </summary>
    /// <remarks>
    /// 取「关闭按钮在右」那一档 —— 那是默认（<c>EditorTabbedContainer.kt:792 ·
    /// isTabActionsOnTheRight</c> 跟着 <c>UISettings.closeTabButtonOnTheRight</c>，默认真）。
    /// </remarks>
    public const double EditorTabContentInsetLeft = 4;

    /// <summary>编辑区标签页**内层**右内边距：expUI 6、Islands 2（后者由主题覆盖）。</summary>
    public const double EditorTabContentInsetRightExpUi = 6;

    /// <summary>同上，Islands 的取值（<c>ManyIslands*.theme.json</c> 的 <c>"0,4,0,2"</c>）。</summary>
    public const double EditorTabContentInsetRightIslands = 2;

    /// <summary>文字到标签页左缘的实际距离 = 两层内边距之和。</summary>
    public const double EditorTabTextInsetLeft = EditorTabInsetHorizontal + EditorTabContentInsetLeft;

    /// <summary>标签页右缘到关闭按钮外侧的距离（两层之和）。</summary>
    /// <param name="islands">是不是 Islands 族。</param>
    /// <returns>右侧总内边距。</returns>
    public static double EditorTabInsetRight(bool islands)
        => EditorTabInsetHorizontal
           + (islands ? EditorTabContentInsetRightIslands : EditorTabContentInsetRightExpUi);

    /// <summary>
    /// 编辑区标签页上，文件图标与标题之间的间距 <b>4</b>
    /// （<c>EditorTabbedContainer.kt:612 · iconTextGap = JBUI.scale(4)</c>）。
    /// </summary>
    public const double EditorTabIconTextGap = 4;

    /// <summary>
    /// 编辑区标签页上文件图标的边长 <b>16</b> —— IDE 的标准图标尺寸
    /// （上游标签页图标走 <c>AllIcons</c>，那套是 16×16）。
    /// </summary>
    public const double EditorTabIconSize = 16;

    /// <summary>
    /// 编辑区标签页的上下内边距 **-7**（同上）。
    /// </summary>
    /// <remarks>负值是真的：新 UI 靠它把标签页压进那 28px 里。照抄，别「修正」成 0。</remarks>
    public const double EditorTabInsetVertical = -7;

    /// <summary>
    /// 编辑区选中标签页的下划线圆角**直径 4**（`EditorTabs.underlineArc`）。
    /// </summary>
    /// <remarks>
    /// ⚠ 代码兜底是 <b>0</b>（<c>JBUI.java:670-671</c>），但**两个族的主题都显式写了 4**
    /// （<c>expUI_dark/light.theme.json</c> 与 <c>islands/ManyIslands*.theme.json</c> 的
    /// <c>EditorTabs.underlineArc</c>），主题覆盖代码，所以实际是 4。
    /// 早先照兜底记成 0，是只看了 JBUI 没去核主题文件。
    /// <para>与其余 arc 一样是**直径**，画的时候折半 —— 见 <see cref="StripeButtonArc"/>。</para>
    /// </remarks>
    public const double EditorTabUnderlineArc = 4;

    /// <summary>
    /// 编辑区选中标签页的下划线高度 **4**（`EditorTabs.underlineHeight`）。
    /// </summary>
    /// <remarks>
    /// 与工具窗口标题条那条（<see cref="HeaderTabUnderlineHeight"/> = 3）**不同**，
    /// 两处各有各的键，别互相借。
    /// </remarks>
    public const double EditorTabUnderlineHeight = 4;

    /// <summary>
    /// Islands 下选中标签页那个圆角块的圆角**直径 12**（`MainToolbar.Button.arc`，
    /// <c>JBUI.java:1416-1418</c>）。
    /// </summary>
    /// <remarks>
    /// 取的是**主工具栏按钮**的圆角，不是标签页自己的键 ——
    /// <c>IslandsTabPainter.kt:169</c> 原文就是 <c>MainToolbar.Button.hoverArc()</c>。
    /// </remarks>
    public const double IslandsEditorTabArc = 12;

    /// <summary>Islands 下选中块的高度 28（紧凑 24，<c>IslandsTabPainter.kt:175</c>）。</summary>
    public const double IslandsEditorTabHeight = 28;

    /// <summary>Islands 下选中块的高度（紧凑）24。</summary>
    public const double IslandsEditorTabHeightCompact = 24;

    /// <summary>Islands 下选中块左右各内缩 4（紧凑 2，<c>IslandsTabPainter.kt:271-276</c> 的非 side 分支）。</summary>
    public const double IslandsEditorTabInsetHorizontal = 4;

    /// <summary>Islands 下选中块左右内缩（紧凑）2。</summary>
    public const double IslandsEditorTabInsetHorizontalCompact = 2;

    /// <summary>Islands 下选中块上下的最小内缩 8（紧凑 4，<c>IslandsTabPainter.kt:173</c> 的非 side 分支）。</summary>
    public const double IslandsEditorTabMinInsetVertical = 8;

    /// <summary>Islands 下选中块上下的最小内缩（紧凑）4。</summary>
    public const double IslandsEditorTabMinInsetVerticalCompact = 4;

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

    /// <summary>图标与名字之间的间距 3（<c>SquareStripeButton.kt:226-227 · iconLabelGap</c>）。</summary>
    public const double StripeIconLabelGap = 3;

    /// <summary>「显示名字」时名字左右的 padding 6（紧凑 4，<c>SquareStripeButton.kt:254</c>）。</summary>
    public const double StripeButtonTextPadding = 6;

    /// <summary>
    /// 从条带按钮起拖的死区 7（<c>MouseDragHelper.java:32 · DRAG_START_DEADZONE</c>）。
    /// </summary>
    /// <remarks>
    /// 条带按钮走的是基类默认值（<c>ToolWindowDragHelper.kt:153-156</c>）；
    /// 其它拖拽源（比如标题条）走 Registry <c>ide.new.tool.window.start.drag.deadzone</c>，
    /// 注册表里写的是 <see cref="HeaderDragStartDeadzone"/>。
    /// </remarks>
    public const double StripeButtonDragStartDeadzone = 7;

    /// <summary>
    /// 从标题条起拖的死区 100（Registry <c>ide.new.tool.window.start.drag.deadzone</c>，
    /// <c>registry.properties:132</c>）。
    /// </summary>
    /// <remarks>代码里写的兜底是 7（<c>ToolWindowDragHelper.kt:157</c>），但注册表文件覆盖成 100。</remarks>
    public const double HeaderDragStartDeadzone = 100;

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
