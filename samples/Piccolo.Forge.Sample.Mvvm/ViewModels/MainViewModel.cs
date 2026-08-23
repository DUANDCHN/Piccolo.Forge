using CommunityToolkit.Mvvm.ComponentModel;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;

namespace Piccolo.Forge.Sample.Mvvm.ViewModels;

/// <summary>
/// 装配处：建面板、登记进外壳、开出编辑区（对位 Dock 示例里 Factory 的角色）。
/// </summary>
/// <remarks>
/// 全应用**唯一**知道"有哪些面板、各停哪条边"的地方。面板之间互不相识，
/// 要通信就在这里把回调（或你的消息总线 / DI 容器）递进去。
/// </remarks>
public sealed class MainViewModel : ObservableObject
{
    /// <summary>建整个布局。</summary>
    public MainViewModel()
    {
        // ① 面板全是 VM。跨面板通信走普通构造注入（真实应用换成你的 DI 容器）。
        var log = new LogPanelViewModel();
        var tasks = new TaskListPanelViewModel(log.Append);

        // ② 登记：把面板本身交出去，身份（Id / Title / Icon）从它自己来；
        //    只说停哪条边 —— 条带按钮画在哪根条带是外壳算出来的。
        Shell = new ToolWindowShellViewModel();
        Shell.Add(tasks, ToolWindowAnchor.Left);
        Shell.Add(log, ToolWindowAnchor.Bottom);

        // ③ 编辑区：塞的同样是 VM，长相同样由 ViewLocator 解析。
        Editor = new EditorAreaViewModel();
        Editor.Open("readme", "README", new ReadmePanelViewModel());

        // ④ 开局摆好。Open 认的是面板 Id。
        Shell.Open("Tasks");
        Shell.Open("Log");
    }

    /// <summary>工具窗口外壳。视图侧把它 Attach 给 ForgeControl。</summary>
    public ToolWindowShellViewModel Shell { get; }

    /// <summary>编辑区。视图侧把它 Attach 给 ForgeEditorControl。</summary>
    public EditorAreaViewModel Editor { get; }
}
