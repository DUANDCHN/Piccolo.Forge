using Avalonia.Controls;

namespace Piccolo.Forge.Sample.Mvvm.Views;

/// <summary>主窗口。对接全在 XAML 的绑定里（见 MainWindow.axaml），这里只有装载。</summary>
public partial class MainWindow : Window
{
    /// <summary>装载 XAML。</summary>
    public MainWindow() => InitializeComponent();
}
