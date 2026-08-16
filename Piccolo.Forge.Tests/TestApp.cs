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
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
