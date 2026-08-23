using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Piccolo.Forge.Avalonia;   // ForgeThemeManager
using Piccolo.Forge.Sample.Shared;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Sample.Islands;

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
                Title = "Piccolo.Forge —— Islands档外壳布局",
                Width = 1100,
                Height = 700,
                Content = ShellDemo.Build(ForgeThemeFamily.Islands),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
