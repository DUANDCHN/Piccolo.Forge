using Avalonia;
using Avalonia.Headless;
using Piccolo.Forge.Tests;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

// Avalonia 的渲染循环是全局的，真 Skia 渲染又要建真 Window，
// 并行跑会撞线程（"The calling thread cannot access this object"）。
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Piccolo.Forge.Tests;

/// headless 测试用的最小 Avalonia 应用。
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<Application>()
            .UseSkia()
            // 关掉 headless 的假绘制，走真正的 Skia，这样能把窗口渲成 PNG 看
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            // Forge 的主题：**不装控件就没有模板**（Avalonia 按实际类型找 ControlTheme，
            // 宿主主题里给 Button 的那份落不到子类头上）。不报错，只是什么都不画
            //——用例里表现为"尺寸对、颜色全无"。宿主侧同样要装，见 REQ-067 / REQ-068。
            //
            // 默认装经典那套；要在用例里换族用 ForgeThemeScope.Use(...)，
            // ⚠ 且必须**先换族再新建控件**（换单元不会让已挂树的控件重取主题，见 ThemeSwapSpikeTests）。
            .AfterSetup(builder => global::Piccolo.Forge.Avalonia.ForgeThemeManager.Apply(
                builder.Instance!, new global::Piccolo.Forge.Themes.ExpUi.ForgeExpUiTheme()));
}
