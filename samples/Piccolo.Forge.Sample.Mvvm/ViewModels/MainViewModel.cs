using System.IO;
using System.Linq;
using System.Xml.Linq;
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
        //    ⚠ 恢复存档必须在**全部 Add 之后**（RestoreLayout 只认已登记的 id），
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
    /// 读存档。两块分开存的（工具窗口的摆放 / 编辑区的标签与分屏树），这里一起恢复。
    /// </summary>
    /// <param name="path">存档路径。</param>
    /// <returns>恢复成功为真；没有存档或存档坏掉为假 —— 调用方接着走 <see cref="OpenDefaultLayout"/>。</returns>
    public bool TryRestoreLayout(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            var root = XElement.Load(path);

            // 工具窗口：摆放套回已登记的面板。存档里多出来的 id 被忽略、缺的保持默认摆放，
            // 这两条库替宿主兜住了（版本升级不炸），见接入指南 §5.5。
            if (root.Element("tools")?.Elements().FirstOrDefault() is { } tools)
                Shell.RestoreLayout(tools);

            // 编辑区：存档里只有标签 id 与分屏结构，**内容由宿主重建** —— 这就是 contentFactory。
            // 认不出来的 id 返回 null，那一页被丢掉并报回（与上游解析不到文件时 warn + 跳过一致）。
            if (root.Element("editor")?.Elements().FirstOrDefault() is { } editor)
                Editor.Restore(editor, CreateEditorContent);

            _log.Append("已恢复上次布局。");
            return true;
        }
        catch (System.Xml.XmlException)
        {
            // 存档坏掉（XML 解析不了）会抛，这是宿主该 catch 的：
            // 记一条、退回默认布局，不要让它挡住启动（接入指南 §5.5 的规矩）。
            _log.Append("布局存档损坏，退回默认布局。");
            return false;
        }
    }

    /// <summary>存档。退出时由 App 调用；用户点"保存布局"之类的入口也是走它。</summary>
    /// <param name="path">存档路径。存到哪、什么时候存是宿主的事，格式是库的事。</param>
    public void SaveLayout(string path)
    {
        // 两块各自的 XML 包在具名容器里再落盘 —— 读的时候按容器取，
        // 不依赖序列化器根元素的名字（空编辑区与分屏编辑区的根元素不同名）。
        var root = new XElement("forge-sample-layout",
            new XElement("tools", Shell.SaveLayout()),
            new XElement("editor", EditorLayoutSerializer.Write(Editor.Area)));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        root.Save(path);
    }

    /// <summary>编辑区恢复时按标签 id 重建内容。新加的页种类在这里补一行。</summary>
    private static object? CreateEditorContent(string id) => id switch
    {
        "readme" => new ReadmePanelViewModel(),
        _ => null,
    };
}
