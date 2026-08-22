namespace Piccolo.Forge.Themes;

/// <summary>
/// 紧凑模式下被替换掉的度量。
/// </summary>
/// <remarks>
/// 上游的做法是「切换密度时把所有 <c>X.compact</c> 的值盖到 <c>X</c> 上」，
/// 一次性改写整张 UIDefaults，而不是在查询时判断
/// （<c>platform/platform-impl/src/com/intellij/ide/ui/laf/LafManagerImpl.kt:1446-1474
/// · applyDensityOnUpdateUi</c>）。
/// <para>
/// 开关是 <c>UISettings.uiDensity</c>，默认 <c>UIDensity.DEFAULT</c>
/// （<c>UISettingsState.kt:144</c>）；<c>UISettings.compactMode</c> 只是它的别名
/// （<c>UISettings.kt:261-265</c>），所以复刻时一个布尔就够。
/// </para>
/// <para>
/// 下面这些值定义在 <c>platform/platform-resources/src/themes/darcula.theme.json</c> ——
/// 它是两条主题链共同的根，所以新 UI 也吃这份值。
/// </para>
/// </remarks>
public static class ForgeCompactMetrics
{
    /// <summary>条带按钮 32×32（`StripeToolbar.Button.size.compact`）。默认 40。</summary>
    public const double StripeButtonSize = 32;

    /// <summary>条带按钮图标 16（`StripeToolbar.Button.iconSize.compact`）。默认 20。</summary>
    public const double StripeButtonIconSize = 16;

    /// <summary>条带按钮图标 padding 4（`StripeToolbar.Button.{left,right}StripeIcon.padding.compact`）。默认 5。</summary>
    public const double StripeButtonIconPadding = 4;

    /// <summary>
    /// 「显示名字」时左条带的图标 padding 3
    /// （`StripeToolbar.Button.leftStripeIconWithName.padding.compact`）。
    /// </summary>
    /// <remarks>
    /// **右条带没有这个紧凑值** —— `rightStripeIconWithName.padding.compact` 在主题里不存在。
    /// <para>
    /// ⚠ <b>缺了 <c>.compact</c> 的后果是「保留基值」，不是「回落到代码兜底」。</b>
    /// 紧凑档的实现是把所有 <c>X.compact</c> 盖到 <c>X</c> 上
    /// （<c>LafManagerImpl.kt:1466-1474 · applyDensityOnUpdateUi</c>），
    /// 没有 <c>.compact</c> 的键根本不参与这次覆盖，于是右条带保留
    /// <c>rightStripeIconWithName.padding</c> 的 **4**——代码兜底的 5 只在键整个缺席时才生效，
    /// 而它并不缺席。此处原先记的是 5，已改正，实际取值见
    /// <see cref="ForgeStripeButtonMetrics.ExpUiCompact"/>。
    /// </para>
    /// </remarks>
    public const double StripeButtonIconPaddingWithNameLeft = 3;

    /// <summary>条带按钮圆角 8（`JBUI.java:1358-1360 · stripeButtonArc(compact)`）。默认 12。</summary>
    public const double StripeButtonArc = 8;

    /// <summary>条带可拖动的最小宽度 33（`ResizeStripeManager.kt:138-141`）。默认 40。</summary>
    public const double StripeMinWidth = 33;

    /// <summary>条带按钮名字的左右 padding 4（`SquareStripeButton.kt:255`）。默认 6。</summary>
    public const double StripeButtonTextPadding = 4;

    /// <summary>标题条高度 31（`ToolWindow.Header.height.compact`）。默认 41。</summary>
    public const double HeaderHeight = 31;

    /// <summary>标题条字号偏移 -1（`ToolWindow.Header.fontSizeOffset.compact`）。默认 0。</summary>
    public const int HeaderFontSizeOffset = -1;
}
