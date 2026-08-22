namespace Piccolo.Forge.Themes;

/// <summary>
/// 上游的主题键名。
/// </summary>
/// <remarks>
/// 权威清单是
/// <c>platform/platform-resources/src/themes/metadata/IntelliJPlatform.themeMetadata.json</c>，
/// 取值示例见 <c>docs/upstream/度量与主题键.md</c> §6。
/// <para>
/// 上游查色是「键 → 兜底键 → 代码字面量」的三级回落
/// （<c>JBColor.namedColor(key, fallback)</c>），复刻时要保留这个链条。
/// </para>
/// </remarks>
public static class ForgeThemeKeys
{
    /// <summary>工具窗口面板背景（<c>JBUI.java:1081-1083</c>）。</summary>
    public const string ToolWindowBackground = "ToolWindow.background";

    /// <summary>工具窗口整体边框色（<c>JBUI.java:1093-1095 · mainBorderColor</c>）。</summary>
    public const string ToolWindowBorderColor = "ToolWindow.borderColor";

    /// <summary>条带背景，兜底 <see cref="ToolWindowBackground"/>（<c>JBUI.java:1085-1087</c>）。</summary>
    public const string StripeBackground = "ToolWindow.Stripe.background";

    /// <summary>条带边框色（<c>JBUI.java:1089-1091</c>）。</summary>
    public const string StripeBorderColor = "ToolWindow.Stripe.borderColor";

    /// <summary>条带上的分组分隔线色（<c>JBUI.java:1148-1152</c>）。</summary>
    public const string StripeSeparatorColor = "ToolWindow.Stripe.separatorColor";

    /// <summary>拖拽中的分组分隔线色（<c>JBUI.java:1148-1152</c>）。</summary>
    public const string StripeDragAndDropSeparatorColor = "ToolWindow.Stripe.DragAndDrop.separatorColor";

    /// <summary>条带按钮前景（<c>StripeButtonUi.kt:27</c>）。</summary>
    public const string StripeButtonForeground = "ToolWindow.Button.foreground";

    /// <summary>条带按钮悬停背景（<c>StripeButtonUi.kt:29-30</c>）。</summary>
    public const string StripeButtonHoverBackground = "ToolWindow.Button.hoverBackground";

    /// <summary>条带按钮选中背景（<c>StripeButtonUi.kt:31-32</c>）。</summary>
    public const string StripeButtonSelectedBackground = "ToolWindow.Button.selectedBackground";

    /// <summary>条带按钮选中前景（<c>StripeButtonUi.kt:33</c>）。</summary>
    public const string StripeButtonSelectedForeground = "ToolWindow.Button.selectedForeground";

    /// <summary>条带按钮尺寸（<c>JBUI.java:1320</c>）。</summary>
    public const string StripeButtonSize = "StripeToolbar.Button.size";

    /// <summary>条带按钮图标尺寸（<c>JBUI.java:1330-1332</c>）。</summary>
    public const string StripeButtonIconSize = "StripeToolbar.Button.iconSize";

    /// <summary>条带按钮圆角（<c>JBUI.java:1358-1360</c>）。</summary>
    public const string StripeButtonArc = "Button.ToolWindow.arc";

    /// <summary>标题条高度（<c>JBUI.java:1174-1176</c>）。</summary>
    public const string HeaderHeight = "ToolWindow.Header.height";

    /// <summary>激活状态的标题条背景（<c>JBUI.java:1166-1169</c>）。</summary>
    public const string HeaderBackground = "ToolWindow.Header.background";

    /// <summary>非激活状态的标题条背景（<c>JBUI.java:1158-1160</c>）。</summary>
    public const string HeaderInactiveBackground = "ToolWindow.Header.inactiveBackground";

    /// <summary>标题条边框色（<c>JBUI.java:1162-1164</c>）。</summary>
    public const string HeaderBorderColor = "ToolWindow.Header.borderColor";

    /// <summary>标题条标题文字左右内边距（<c>JBUI.java:1182-1184</c>）。</summary>
    public const string HeaderLabelLeftRightInsets = "ToolWindow.Header.labelLeftRightInsets";

    /// <summary>标题条工具栏左右内边距（<c>JBUI.java:1186-1188</c>）。</summary>
    public const string HeaderToolbarLeftRightInsets = "ToolWindow.Header.toolbarLeftRightInsets";

    /// <summary>标签页左右内边距（<c>JBUI.java:1133-1135</c>）。</summary>
    public const string HeaderTabLeftRightInsets = "ToolWindow.HeaderTab.leftRightInsets";

    /// <summary>选中标签页下划线高度（<c>JBUI.java:1190-1192</c>）。</summary>
    public const string HeaderTabUnderlineHeight = "ToolWindow.HeaderTab.underlineHeight";

    /// <summary>选中标签页下划线圆角（<c>JBUI.java:1129-1131</c>）。</summary>
    public const string HeaderTabUnderlineArc = "ToolWindow.HeaderTab.underlineArc";

    /// <summary>选中标签页下划线色（<c>JBUI.java:1109-1111</c>）。</summary>
    public const string HeaderTabUnderlineColor = "ToolWindow.HeaderTab.underlineColor";
}
