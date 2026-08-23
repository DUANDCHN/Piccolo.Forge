using CommunityToolkit.Mvvm.ComponentModel;
using Piccolo.Forge.Mvvm.CommunityToolkit.Controls;

namespace Piccolo.Forge.Sample.Mvvm.ViewModels;

/// <summary>
/// 编辑区面板。**继承 <see cref="EditorPanel"/>，这个 VM 就活在中间那块** —— 一页说明文字。
/// </summary>
public sealed partial class ReadmePanelViewModel : EditorPanel
{
    /// <summary>建说明页。</summary>
    public ReadmePanelViewModel() : base("readme")
    {
        Title = "README";
        _body = """
            这是 MVVM 接法的演示（结构照 Dock 的 MvvmSample）：

            · 左侧「任务」与底部「日志」各是一个继承 ToolWindowPanel 的 VM
            · 本页是一个继承 EditorPanel 的 VM，活在编辑区
            · 三个 VM 的长相都在 Views/ 目录的 axaml 里，由 ViewLocator 按名字解析
            · 命令、CanExecute、变更通知由 CommunityToolkit 的源生成器生成

            试试：左侧点「添加」，底部日志会跟着出一行 ——
            两块面板互不相识，通信走构造注入的回调。
            """;
    }

    /// <summary>正文。演示编辑区面板同样可绑。</summary>
    [ObservableProperty]
    private string _body;
}
