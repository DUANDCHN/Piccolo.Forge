using System.Xml;
using System.Xml.Linq;
using Piccolo.Forge.Model;

namespace Piccolo.Forge.Mvvm;

/// <summary>
/// 整份布局的一站式存读：工具窗口的摆放 + 编辑区的标签与分屏树，一个根元素、一对调用
/// （对位 Dock 的 <c>DockSerializer</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 底下仍是那两个分块的序列化器（<see cref="ToolWindowLayoutSerializer"/> /
/// <see cref="EditorLayoutSerializer"/>），它们照旧公开 —— 只存一块、或要把两块埋进
/// 自己设置文件不同位置的宿主继续用它们。本类吃掉的是**每个宿主都得抄一遍的包装**：
/// 两块各自包进具名容器（编辑区的根元素随分屏与否变名字，不包就没法按名取）、
/// 坏档的 try/catch、以及"恢复要在全部 Add 之后"之外没有别的顺序要求这件事。
/// </para>
/// <para>用法（存到哪、什么时候存是宿主的事）：</para>
/// <code>
/// // 存 —— 退出或用户点「保存布局」时
/// ForgeLayoutSerializer.Write(shell, editor).Save(path);
///
/// // 读 —— 全部 Add 之后；没有存档、存档坏掉、根元素不认识都返回 false，接着走默认摆放
/// if (!ForgeLayoutSerializer.TryRead(path, shell, editor, id => Reopen(id)))
///     OpenDefaultLayout();
/// </code>
/// </remarks>
public static class ForgeLayoutSerializer
{
    /// <summary>根元素名。</summary>
    public const string RootTag = "forge-layout";

    /// <summary>工具窗口那一块的容器名。</summary>
    public const string ToolsTag = "tools";

    /// <summary>编辑区那一块的容器名。</summary>
    public const string EditorTag = "editor";

    /// <summary>把两块布局写成一个根元素。落盘、进设置系统、发到别处，随宿主。</summary>
    /// <param name="shell">工具窗口外壳。</param>
    /// <param name="editor">编辑区。</param>
    /// <returns>一个 <see cref="RootTag"/> 根元素，两块各在自己的具名容器里。</returns>
    public static XElement Write(ToolWindowShellViewModel shell, EditorAreaViewModel editor) =>
        new(RootTag,
            new XAttribute("version", 1),
            new XElement(ToolsTag, shell.SaveLayout()),
            new XElement(EditorTag, EditorLayoutSerializer.Write(editor.Area)));

    /// <summary>
    /// 从文件恢复整份布局。**必须在全部 <c>Add</c> 之后调**（摆放只认已登记的面板 id）。
    /// </summary>
    /// <param name="path">存档路径。</param>
    /// <param name="shell">工具窗口外壳。</param>
    /// <param name="editor">编辑区。</param>
    /// <param name="contentFactory">编辑区按标签 id 重建内容；认不出返回 <c>null</c>，那一页被丢掉。</param>
    /// <returns>
    /// 恢复成功为真。没有存档、XML 解析不了、根元素不认识都为假且**不抛** ——
    /// 调用方唯一要做的事是假时走默认摆放。存档坏掉不该挡住启动。
    /// </returns>
    public static bool TryRead(
        string path,
        ToolWindowShellViewModel shell,
        EditorAreaViewModel editor,
        System.Func<string, object?>? contentFactory = null)
    {
        if (!System.IO.File.Exists(path))
            return false;

        XElement root;
        try
        {
            root = XElement.Load(path);
        }
        catch (XmlException)
        {
            return false;
        }

        return TryRead(root, shell, editor, contentFactory);
    }

    /// <summary>
    /// 从已解析的根元素恢复（存档埋在宿主自己设置文件里的场合）。约束同上：全部 <c>Add</c> 之后调。
    /// </summary>
    /// <param name="root">当初 <see cref="Write"/> 出来的根元素。</param>
    /// <param name="shell">工具窗口外壳。</param>
    /// <param name="editor">编辑区。</param>
    /// <param name="contentFactory">编辑区按标签 id 重建内容。</param>
    /// <returns>根元素不是 <see cref="RootTag"/> 为假；两块各自缺失时跳过那一块，不算失败。</returns>
    public static bool TryRead(
        XElement root,
        ToolWindowShellViewModel shell,
        EditorAreaViewModel editor,
        System.Func<string, object?>? contentFactory = null)
    {
        if (root.Name.LocalName != RootTag)
            return false;

        // 版本升级的兜底在分块序列化器里（多出来的 id 忽略、缺的保持默认），这里不重复把关。
        if (root.Element(ToolsTag)?.Elements().FirstOrDefault() is { } tools)
            shell.RestoreLayout(tools);

        if (root.Element(EditorTag)?.Elements().FirstOrDefault() is { } area)
            editor.Restore(area, contentFactory);

        return true;
    }
}
