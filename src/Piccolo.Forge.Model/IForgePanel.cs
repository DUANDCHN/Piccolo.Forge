namespace Piccolo.Forge.Model;

/// <summary>
/// 一块能放进外壳的面板。
/// </summary>
/// <remarks>
/// 这是**库对宿主内容的全部要求**：给个身份、给个名字、给个图标。
/// 别的一概不管 —— 面板里装什么、用什么 MVVM 框架、视图怎么解析，都是宿主的事。
/// <para>
/// 不要直接实现它，实现 <see cref="IToolWindowPanel"/> 或 <see cref="IEditorPanel"/> 之一：
/// <b>面板停在哪由类型决定</b>，不需要宿主每次去说。
/// </para>
/// </remarks>
public interface IForgePanel
{
    /// <summary>面板 id，全局唯一，存档按它认账。</summary>
    string Id { get; }

    /// <summary>显示名：条带按钮的提示、标题条上的标题。</summary>
    string Title { get; }

    /// <summary>
    /// 条带按钮 / 标签页上的图标。
    /// </summary>
    /// <remarks>
    /// 是 <see cref="object"/> 而不是某个图形类型：Forge <b>不带图标资源</b>
    /// （上游那套是 JetBrains 的美术资产，见 <c>工具窗口模型.md</c> §17），
    /// 也不想强迫宿主用某个图标库。宿主塞控件、几何图形还是字符都行。
    /// </remarks>
    object? Icon { get; }
}

/// <summary>
/// 停在四条边上的工具面板。
/// </summary>
/// <remarks>
/// <b>这是个标记接口，故意没有成员。</b>
/// 它要表达的事只有一件：这块面板停在边上、由条带按钮开合、遵守
/// 「一格一个可见窗口」的互斥（<c>工具窗口模型.md</c> §7.1）。
/// <para>
/// 具体停哪条边、哪一半，登记时说一次即可（<c>anchor + isSplit</c>）；
/// 按钮画在哪根条带是**算出来的**，宿主永远不用管。
/// </para>
/// <para>
/// 与 <see cref="IEditorPanel"/> 在类型上分开，是为了让「把编辑器面板停到侧边」
/// 这种事在<b>编译期</b>就不成立 —— 上游也是这么分的，工具窗口子系统对文档一无所知
/// （<c>ToolWindowPane.kt:392 · setDocumentComponent</c> 只是个槽）。
/// </para>
/// </remarks>
public interface IToolWindowPanel : IForgePanel;

/// <summary>
/// 活在中间编辑区的面板。
/// </summary>
/// <remarks>
/// 同样是标记接口。上游的中间区只是 <c>ToolWindowPane</c> 的一个槽
/// （<c>ToolWindowPane.kt:392 · setDocumentComponent</c>），
/// 文档怎么排、要不要标签页，是**另一个子系统**的事（<c>FileEditorManager</c>）。
/// Forge 保持同样的边界：只认「这是编辑区的内容」，不替宿主管标签页与分屏。
/// </remarks>
public interface IEditorPanel : IForgePanel;
