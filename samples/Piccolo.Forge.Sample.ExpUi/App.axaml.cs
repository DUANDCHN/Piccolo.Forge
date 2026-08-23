using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Piccolo.Forge.Avalonia;   // ForgeThemeManager
using Piccolo.Forge.Sample.Shared;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Sample.ExpUi;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // 文案跟系统语言；也可以传 "zh-CN" / "ja-JP" 这样的名字。
        ForgeThemeManager.ApplyLocale(this, CultureInfo.CurrentUICulture);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window
            {
                Title = "Piccolo.Forge —— 经典（expUI）档外壳布局",
                Width = 1100,
                Height = 700,
                Content = ShellDemo.Build(ForgeThemeFamily.ExpUi),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
