using CommunityToolkit.Mvvm.ComponentModel;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;

namespace Piccolo.Forge.Sample.Mvvm.ViewModels;

/// <summary>
/// 装配处：建面板、登记进外壳、管布局的存与读（对位 Dock 示例里 Factory 的角色）。
/// </summary>
/// <remarks>
/// 全应用**唯一**知道"有哪些面板、各停哪条边"的地方。面板之间互不相识，
/// 要通信就在这里把回调（或你的消息总线 / DI 容器）递进去。
/// </remarks>
public sealed class MainViewModel : ObservableObject
{
    private readonly LogPanelViewModel _log;

    /// <summary>建面板并登记。开局摆放不在这里 —— 由 App 按"有没有存档"决定走哪条。</summary>
    public MainViewModel()
    {
        // ① 面板全是 VM。跨面板通信走普通构造注入（真实应用换成你的 DI 容器）。
        _log = new LogPanelViewModel();
        var tasks = new TaskListPanelViewModel(_log.Append);

        // ② 登记：把面板本身交出去，身份（Id / Title / Icon）从它自己来；
        //    只说停哪条边 —— 条带按钮画在哪根条带是外壳算出来的。
        //    ⚠ 恢复存档必须在**全部 Add 之后**（摆放只认已登记的 id），
        //      所以登记在构造里、恢复在 TryRestoreLayout 里，天然分了先后。
        Shell = new ToolWindowShellViewModel();
        Shell.Add(tasks, ToolWindowAnchor.Left);
        Shell.Add(_log, ToolWindowAnchor.Bottom);

        Editor = new EditorAreaViewModel();
    }

    /// <summary>工具窗口外壳。视图侧绑给 <c>ForgeControl.Shell</c>。</summary>
    public ToolWindowShellViewModel Shell { get; }

    /// <summary>编辑区。视图侧绑给 <c>ForgeEditorControl.Editor</c>。</summary>
    public EditorAreaViewModel Editor { get; }

    /// <summary>没有存档（首次启动 / 存档坏掉）时的默认摆放。</summary>
    public void OpenDefaultLayout()
    {
        Editor.Open("readme", "README", new ReadmePanelViewModel());
        Shell.Open("Tasks");
        Shell.Open("Log");
        _log.Append("首次启动，使用默认布局。");
    }

    /// <summary>
    /// 读存档。<see cref="ForgeLayoutSerializer"/> 一次恢复两块（工具窗口摆放 + 编辑区标签/分屏树）；
    /// 没有存档、存档坏掉都返回假且不抛 —— 调用方接着走 <see cref="OpenDefaultLayout"/> 即可。
    /// </summary>
    /// <param name="path">存档路径。</param>
    /// <returns>恢复成功为真。</returns>
    public bool TryRestoreLayout(string path)
    {
        // 编辑区内容按标签 id 重建（存档里只有 id 与结构）——新加的页种类在 switch 里补一行。
        if (!ForgeLayoutSerializer.TryRead(path, Shell, Editor, CreateEditorContent))
            return false;

        _log.Append("已恢复上次布局。");
        return true;
    }

    /// <summary>存档。退出时由 App 调用；用户点"保存布局"之类的入口也是走它。</summary>
    /// <param name="path">存档路径。存到哪、什么时候存是宿主的事，格式是库的事。</param>
    public void SaveLayout(string path)
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        ForgeLayoutSerializer.Write(Shell, Editor).Save(path);
    }

    /// <summary>编辑区恢复时按标签 id 重建内容。认不出的 id 返回 null，那一页被丢掉并报回。</summary>
    private static object? CreateEditorContent(string id) => id switch
    {
        "readme" => new ReadmePanelViewModel(),
        _ => null,
    };
}
