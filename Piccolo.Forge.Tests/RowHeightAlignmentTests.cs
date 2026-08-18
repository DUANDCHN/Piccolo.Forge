using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using System.Linq;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 面板标题条与文档标签行**同高**。
/// </summary>
/// <remarks>
/// <see cref="ForgeAppearance.HeaderHeight"/> 与 <see cref="ForgeAppearance.TabRowHeight"/>
/// 是同一件事的两半：宿主给同一个数，意思就是这两行一样高、左右看过去是一条线。
/// <para>
/// 曾经差 1px —— 标签条底下那条分隔线用 <c>BorderThickness</c> 画，**占了高度**，
/// 于是文档标签行总是「行高 + 1」。差一个像素在截图上就是"文档区的标题栏比面板区高"。
/// 改成压在内容上之后这里钉住它。
/// </para>
/// </remarks>
public class RowHeightAlignmentTests
{
    [AvaloniaTheory]
    [InlineData(31)]   // 本软件用的紧凑档
    [InlineData(40)]
    public void 给同一个行高时两行的外沿一样高(double rowHeight)
    {
        var shell = new ToolWindowShellViewModel();
        shell.Add("A", "甲", ToolWindowAnchor.Left, false);
        shell.Manager.Activate("A");

        var editor = new EditorAreaViewModel();
        editor.Open("a", "A.cs");

        var frame = new ForgeControl();
        frame.Attach(shell);
        var editorControl = new ForgeEditorControl();
        editorControl.Attach(editor);
        frame.EditorContent = editorControl;
        frame.Appearance = ForgeAppearance.Default with { HeaderHeight = rowHeight, TabRowHeight = rowHeight };

        var window = new Window { Width = 1000, Height = 600, Content = frame };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var header = frame.GetVisualDescendants().OfType<ForgeToolWindowHeader>().First();
        var tab = editorControl.GetVisualDescendants().OfType<ForgeEditorTab>().First();

        // 标签条整体（含底下那条线）——标签的第一个 Border 祖先就是它的宿主
        var stripHost = tab.GetVisualAncestors().OfType<Border>().First();

        Assert.Equal(rowHeight, header.Bounds.Height);
        Assert.Equal(rowHeight, stripHost.Bounds.Height);
    }
}
