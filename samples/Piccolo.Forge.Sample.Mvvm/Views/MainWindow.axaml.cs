using Avalonia.Controls;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Sample.Mvvm.ViewModels;

namespace Piccolo.Forge.Sample.Mvvm.Views;

/// <summary>
/// 主窗口：把 <see cref="MainViewModel"/> 的 Shell / Editor 对接给外框。
/// </summary>
/// <remarks>
/// Attach 是命令式 API，所以这几行住在 code-behind —— 这不违背 MVVM：
/// 装配处本来就允许碰控件，要紧的是**面板与业务 VM 里没有控件代码**。
/// </remarks>
public partial class MainWindow : Window
{
    /// <summary>建主窗口并完成对接。</summary>
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // 编辑区控件接编辑区 VM，塞进外框；外框再接外壳 VM。
        var editorControl = new ForgeEditorControl();
        editorControl.Attach(vm.Editor);
        Frame.EditorContent = editorControl;
        Frame.Attach(vm.Shell);
    }
}
