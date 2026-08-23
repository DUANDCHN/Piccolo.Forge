using System.Xml.Linq;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Mvvm.Controls;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 一站式存读（<see cref="ForgeLayoutSerializer"/>）：一个根元素、一对调用，坏档不抛只返回假。
/// </summary>
/// <remarks>
/// 分块序列化器各自的行为（版本兜底、只写非默认值）有自己的用例，这里只测**门面新增的**：
/// 容器包装、整体回环、坏档三态（缺文件 / 解析不了 / 根元素不认识）。
/// </remarks>
public class ForgeLayoutSerializerTests
{
    private sealed class Tool(string id) : ToolWindowPanel(id);

    private static (ToolWindowShellViewModel shell, EditorAreaViewModel editor) NewPair()
    {
        var shell = new ToolWindowShellViewModel();
        shell.Add(new Tool("A"), ToolWindowAnchor.Left);
        shell.Add(new Tool("B"), ToolWindowAnchor.Bottom);
        var editor = new EditorAreaViewModel();
        return (shell, editor);
    }

    [Fact]
    public void 整体回环_摆放与编辑区标签都回来()
    {
        var (saved, savedEditor) = NewPair();
        savedEditor.Open("doc-1", "Doc 1", "内容1");
        savedEditor.Open("doc-2", "Doc 2", "内容2");
        saved.Open("B");

        var xml = ForgeLayoutSerializer.Write(saved, savedEditor);

        var (restored, restoredEditor) = NewPair();
        Assert.True(ForgeLayoutSerializer.TryRead(xml, restored, restoredEditor, id => $"重建:{id}"));

        // 工具窗口：B 打开着的状态跟着回来
        Assert.True(restored.ToolWindows.First(w => w.Id == "B").IsOpen);
        // 编辑区：两页都在，内容是 contentFactory 重建的（Tabs 挂在组上，区上是 Groups）
        var tabs = restoredEditor.Groups.SelectMany(g => g.Tabs).ToList();
        Assert.Equal(2, tabs.Count);
        Assert.Equal("重建:doc-1", tabs.First(t => t.Id == "doc-1").Content);
    }

    [Fact]
    public void 根元素带版本号与两个具名容器()
    {
        var (shell, editor) = NewPair();
        var xml = ForgeLayoutSerializer.Write(shell, editor);

        Assert.Equal(ForgeLayoutSerializer.RootTag, xml.Name.LocalName);
        Assert.NotNull(xml.Attribute("version"));
        // 容器包装是这个门面的存在理由之一：编辑区的根元素随分屏与否变名字，按容器取才稳
        Assert.NotNull(xml.Element(ForgeLayoutSerializer.ToolsTag));
        Assert.NotNull(xml.Element(ForgeLayoutSerializer.EditorTag));
    }

    [Fact]
    public void 缺文件_返回假不抛()
    {
        var (shell, editor) = NewPair();
        Assert.False(ForgeLayoutSerializer.TryRead(
            Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), shell, editor));
    }

    [Fact]
    public void 存档坏掉_返回假不抛()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(path, "<forge-layout><tools>没关上");
        try
        {
            var (shell, editor) = NewPair();
            Assert.False(ForgeLayoutSerializer.TryRead(path, shell, editor));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void 根元素不认识_返回假不动布局()
    {
        var (shell, editor) = NewPair();
        shell.Open("A");

        Assert.False(ForgeLayoutSerializer.TryRead(new XElement("别家的存档"), shell, editor));
        // 布局没被动过
        Assert.True(shell.ToolWindows.First(w => w.Id == "A").IsOpen);
    }

    [Fact]
    public void 只有一块也能读_另一块保持不动()
    {
        var (saved, savedEditor) = NewPair();
        saved.Open("A");
        var xml = ForgeLayoutSerializer.Write(saved, savedEditor);
        xml.Element(ForgeLayoutSerializer.EditorTag)!.Remove();   // 假装老版本只存了工具窗口

        var (restored, restoredEditor) = NewPair();
        restoredEditor.Open("keep", "Keep", "还在");

        Assert.True(ForgeLayoutSerializer.TryRead(xml, restored, restoredEditor));
        Assert.True(restored.ToolWindows.First(w => w.Id == "A").IsOpen);
        Assert.Single(restoredEditor.Groups.SelectMany(g => g.Tabs));   // 编辑区那块缺失 → 跳过，不清空
    }
}
