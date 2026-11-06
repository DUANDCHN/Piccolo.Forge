using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>布局槽位缓存只能持有仍在面板中的标签，不能延长已关闭文档的寿命。</summary>
public class HeaderTabsLifetimeTests
{
    /// <summary>保持编辑区存活，反复关闭同名文档，覆盖图标工厂、布局槽位及旧分组缓存。</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void 六个文档开关六轮后旧标签分组与内容均可回收(bool split)
    {
        using var theme = ForgeThemeScope.Use(ForgeThemeFamily.Islands);
        var editor = new EditorAreaViewModel();
        var control = new ForgeEditorControl();
        control.Attach(editor);
        var window = new Window { Width = 900, Height = 700, Content = control };
        window.Show();
        try
        {
            var references = new List<WeakReference>();
            for (var round = 0; round < 6; round++)
                OpenAndClose(editor, control, window, references, split);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.All(references, reference => Assert.False(reference.IsAlive, "编辑区缓存仍持有已关闭文档的对象。"));
            GC.KeepAlive(control);
        }
        finally { window.Close(); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void OpenAndClose(EditorAreaViewModel editor, ForgeEditorControl control,
        Window window, List<WeakReference> references, bool split)
    {
        for (var i = 0; i < 6; i++)
        {
            var content = new Border { Tag = new byte[1024 * 1024] };
            var tab = editor.Open($"image-{i}", $"image-{i}.png", content);
            // 图标工厂与上位机一致，每个标签容器各持有自己的图标实例。
            tab.Icon = (Func<Control>)(() => new Border { Width = 16, Height = 16 });
            Render();
            references.Add(new WeakReference(tab));
            references.Add(new WeakReference(content));
        }
        // 分屏路径额外覆盖移除旧组时的按钮与接线缓存，单组路径复现上位机操作。
        if (split)
        {
            editor.Area.ArrangeGrid(Enumerable.Range(0, 6).Select(i => $"image-{i}").ToArray(), 0, 0);
            Render();
        }
        var groups = editor.Groups.ToArray();
        foreach (var tab in control.GetVisualDescendants().OfType<ForgeEditorTab>())
            references.Add(new WeakReference(tab));
        for (var i = 0; i < 6; i++)
        {
            Assert.True(editor.Close($"image-{i}"));
            Render();
        }
        // 最后一个空组可继续复用；只有退出模型的分组才应回收。
        foreach (var group in groups)
            if (!editor.Groups.Contains(group)) references.Add(new WeakReference(group));

        void Render()
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            using var frame = window.CaptureRenderedFrame();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void 删除标签后布局缓存立即放开引用(bool wrap)
    {
        var panel = new HeaderTabsPanel { WrapRows = wrap };
        var removed = AddArrangeAndRemove(panel);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(removed.IsAlive, "槽位字典不应强引用已删除的标签。");
        GC.KeepAlive(panel);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AddArrangeAndRemove(HeaderTabsPanel panel)
    {
        var tab = new ForgeEditorTab { Title = "image.png", Tag = new byte[1024 * 1024] };
        panel.Children.Add(tab);
        panel.Measure(new Size(600, 40));
        panel.Arrange(new Rect(0, 0, 600, 40));
        Assert.NotNull(panel.SlotOf(tab));
        var reference = new WeakReference(tab);
        panel.Children.Remove(tab);
        Assert.Null(panel.SlotOf(tab));
        return reference;
    }
}
