using Avalonia;
using Avalonia.Headless;
using Piccolo.Forge.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Piccolo.Forge.Tests;

/// headless 测试用的最小 Avalonia 应用。
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
