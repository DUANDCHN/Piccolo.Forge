using Avalonia;

namespace Piccolo.Forge.Sample;

/// 示例程序入口。
internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// 供 Avalonia 设计器与 headless 复用的 AppBuilder。
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<SampleApp>()
            .UsePlatformDetect()
            .LogToTrace();
}
