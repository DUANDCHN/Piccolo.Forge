using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 挂在标题**后面**的那个宿主控件（<c>ForgeControl.HeaderTitleAdornmentExtension</c>）。
/// </summary>
/// <remarks>
/// <b>为什么要单独开一个位，而不是让宿主拿 <c>SetExtraButtons</c> 顶</b>：右侧那条工具栏
/// 把每个控件都排成 <c>ForgeMetrics.HeaderButtonSize</c> 见方的格子（<c>ArrangeContent</c>），
/// 宽度由库定死。宿主往里塞"图标 + 一行字"，字会溢出格子压在旁边按钮上 ——
/// 这不是宿主用错了，是那个位本来就只放得下图标按钮。
/// <para>
/// 所以这一组的判据集中在一件事上：<b>这个位的宽度由控件自己说了算</b>，
/// 而且标签条要跟着往右让 —— 不让就是换个地方重演同一个重叠。
/// </para>
/// <para>
/// 位置的依据见 <c>docs/upstream/工具窗口模型.md</c> §13：上游 <c>ToolWindowHeader</c> 的 westPanel
/// 里标题与内容标签本来就是同一行，这个槽插在两者之间。
/// </para>
/// </remarks>
public class HeaderTitleAdornmentTests
{
    [AvaloniaFact]
    public void 不挂东西时标签起点和从前一样()
    {
        var (window, frame, _) = Build();
        var bare = Header(frame).ComputeTabsStart();

        var (window2, frame2, _) = Build(_ => null);

        Assert.Equal(bare, Header(frame2).ComputeTabsStart());
        window2.Close();
        window.Close();
    }

    [AvaloniaFact]
    public void 挂上去以后按控件自己量出来的宽度排在标题后面()
    {
        var tag = new Border { Width = 96, Height = 18 };
        var (bareWindow, bareFrame, _) = Build();
        var bare = Header(bareFrame).ComputeTabsStart();
        bareWindow.Close();

        var (window, frame, _) = Build(_ => tag);
        var header = Header(frame);

        // ① 排在标题右边、标签左边
        var x = tag.TranslatePoint(default, header)!.Value.X;
        Assert.True(x >= bare - 0.5, $"标题后缀跑到标题里去了（x={x}，标题结束于 {bare}）");

        // ② **宽度是它自己的**，不是被压成 HeaderButtonSize 那个方格
        Assert.Equal(96, tag.Bounds.Width, 1);
        Assert.NotEqual(ForgeMetrics.HeaderButtonSize, tag.Bounds.Width);

        // ③ 标签条跟着往右让 —— 不让就是换个地方重演重叠
        Assert.True(Header(frame).ComputeTabsStart() >= x + tag.Bounds.Width,
            "标签条压在标题后缀上了");

        window.Close();
    }

    [AvaloniaFact]
    public void 藏起来时不占位()
    {
        var tag = new Border { Width = 96, Height = 18 };
        var (bareWindow, bareFrame, _) = Build();
        var bare = Header(bareFrame).ComputeTabsStart();
        bareWindow.Close();

        var (window, frame, _) = Build(_ => tag);
        var occupied = Header(frame).ComputeTabsStart();

        tag.IsVisible = false;
        Render(window);

        Assert.True(occupied > bare, "挂上之后本来就该占位");
        Assert.Equal(bare, Header(frame).ComputeTabsStart());
        window.Close();
    }

    [AvaloniaFact]
    public void 同一个实例重复给不会重复加进槽()
    {
        // 每次同步都会问一次（与 SetExtraButtons 同约定）；宿主按 id 缓存后给回同一个实例，
        // 库这边必须认得出来，否则一个控件被反复摘挂。
        var tag = new Border { Width = 40, Height = 18 };
        var (window, frame, _) = Build(_ => tag);
        Render(window);

        var header = Header(frame);
        Assert.Single(header.GetVisualDescendants().OfType<Border>(), b => ReferenceEquals(b, tag));
        window.Close();
    }

    [AvaloniaFact]
    public void 面板窄时按剩余空间量_不许压到右侧按钮上()
    {
        // 这条守的是这个位存在的理由本身：右侧那条工具栏一律按 24 见方排、不问控件多宽，
        // 宿主放"图标 + 一行字"就会溢出压在按钮上。搬到这个位来，如果宽度还是不受约束，
        // 那就是换个地方重演同一件事。
        //
        // ⚠ 宿主侧对应的坑：横排 StackPanel 给子控件的可用宽度是**无限**，
        //   会把这里传下去的"还剩多少"整个吃掉。会缩的容器（DockPanel / 带约束的 Grid）才作数。
        var text = new TextBlock
        {
            Text = "移动竖线测试图 1280×1024 8bit 这串名字长得离谱就是要撑爆标题条",
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var (window, frame, _) = Build(_ => text, panelWeight: 0.18f);
        var header = Header(frame);

        var buttonsLeft = header.GetVisualDescendants().OfType<ForgeHeaderButton>()
            .Where(b => b.IsVisible)
            .Select(b => b.TranslatePoint(default, header)!.Value.X)
            .Min();

        // 先确认它真的排上了 —— 否则"没压到按钮"可以靠根本没画出来蒙混过关
        Assert.True(text.Bounds.Width > 0, "标题后缀压根没排上，这条就白测了");

        var right = text.TranslatePoint(default, header)!.Value.X + text.Bounds.Width;
        Assert.True(right <= buttonsLeft + 0.5,
            $"标题后缀压到右侧按钮上了（右边缘 {right}，按钮从 {buttonsLeft} 开始）");

        window.Close();
    }

    private static ForgeToolWindowHeader Header(ForgeControl frame)
        => frame.GetVisualDescendants().OfType<ForgeToolWindowHeader>().Single();

    /// <remarks>
    /// ⚠ 挂载点必须在 <c>Attach</c> <b>之前</b>设：库只在布局同步时问一次，
    /// 接上之后再设不会自己重来一遍（同 <c>HeaderButtonsTests.Build</c> 的 configure 时机）。
    /// </remarks>
    private static (Window Window, ForgeControl Frame, ToolWindowShellViewModel Shell) Build(
        Func<string, Control?>? adornment = null,
        float panelWeight = 0.6f)
    {
        var infos = new List<WindowInfo>
        {
            // 面板要够宽：这个位的可用空间是"标题右边到按钮左边之间还剩多少"，
            // 窄面板下控件会被按剩余空间量（那是对的行为，见「放不下时按剩余空间量」那条）。
            new() { Id = "相机列表", Anchor = ToolWindowAnchor.Left, Order = 0, IsVisible = true, Weight = panelWeight },
        };

        var shell = new ToolWindowShellViewModel(new ToolWindowManager(new DesktopLayout(infos)));
        var frame = new ForgeControl
        {
            Palette = ForgePalette.ExpUiDark,
            Appearance = ForgeAppearance.Default with { FollowThemeVariant = false },
        };
        frame.HeaderTitleAdornmentExtension = adornment;
        frame.Attach(shell);

        var window = new Window { Width = 1200, Height = 300, WindowDecorations = WindowDecorations.None, Content = frame };
        window.Show();
        Render(window);
        return (window, frame, shell);
    }

    private static void Render(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
    }
}
