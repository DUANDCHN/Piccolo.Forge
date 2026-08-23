using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Piccolo.Forge.Sample.Mvvm.ViewModels;
using Piccolo.Forge.Sample.Mvvm.Views;

namespace Piccolo.Forge.Sample.Mvvm;

/// <summary>应用入口：装主题（XAML 里）、建装配处 VM、接上布局持久化、开主窗口。</summary>
public partial class App : Application
{
    /// <summary>布局存档的位置。存到哪、什么时候存是宿主的事 —— 这里选每用户数据目录。</summary>
    private static string LayoutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Piccolo.Forge.Sample.Mvvm", "layout.xml");

    /// <summary>装载 App.axaml（主题与 ViewLocator 都在那里）。</summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel();

            // 启动：有存档就恢复（登记已在 VM 构造里做完，满足"恢复须在全部 Add 之后"），
            // 没有或坏掉就走默认摆放 —— 两条路必走其一，不会出现空布局。
            if (!vm.TryRestoreLayout(LayoutPath))
                vm.OpenDefaultLayout();

            // 退出：把当下的摆放存回去。拖过的面板宽度、开合状态、编辑区分屏都在里面。
            desktop.ShutdownRequested += (_, _) => vm.SaveLayout(LayoutPath);

            // 装配处 VM 给到 DataContext，窗口里的一切对接靠绑定。
            desktop.MainWindow = new MainWindow { DataContext = vm };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
