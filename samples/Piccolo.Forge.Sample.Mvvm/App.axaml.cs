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
            desktop.MainWindow = new MainWindow(new MainViewModel());

        base.OnFrameworkInitializationCompleted();
    }
}
