using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Piccolo.Forge.Sample.Mvvm.ViewModels;
using Piccolo.Forge.Sample.Mvvm.Views;

namespace Piccolo.Forge.Sample.Mvvm;

/// <summary>应用入口：装主题（XAML 里）、建装配处 VM、开主窗口。</summary>
public partial class App : Application
{
    /// <summary>装载 App.axaml（主题与 ViewLocator 都在那里）。</summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            // 装配处 VM 给到 DataContext，窗口里的一切对接靠绑定。
            desktop.MainWindow = new MainWindow { DataContext = new MainViewModel() };

        base.OnFrameworkInitializationCompleted();
    }
}
