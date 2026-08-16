using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 编辑区两格之间那条分割线。
/// </summary>
/// <remarks>
/// 回归：原实现用的是 Avalonia 自带的 <c>GridSplitter</c>，长相全看宿主装了哪套主题 ——
/// 结果是**分屏之后中间根本没有线**；而且它拖完只改 Grid、不写 <see cref="EditorSplit.Proportion"/>，
/// 下一次重建就把用户拖出来的比例打回原样。
/// </remarks>
public class EditorSplitterTests
{
    [AvaloniaFact]
    public void 分屏之间画得出一条线而且看得见()
    {
        var (window, area, vm) = Build();
        vm.Area.ArrangeGrid(["d0", "d1"], 0, 0);
        Render(window);

        var splitter = Assert.Single(area.GetVisualDescendants().OfType<ForgeEditorSplitter>());

        // 左右分屏 → 竖着的一条：版面上 1px，命中区 6px
        Assert.False(splitter.IsVertical);
        Assert.Equal(ForgeEditorSplitter.MouseZone, splitter.Bounds.Width, 3);
        Assert.Equal(1d, ForgeEditorSplitter.LineRect(splitter.Bounds.Size, isVertical: false).Width, 3);

        // 真的画出来了 —— 断言的是用户的诉求本身：**这条缝看得见**。
        // 不比精确色值：1px 的线落在半像素上时会与底色做抗锯齿混合，
        // 比色值等于把断言绑在渲染细节上。判据取「这一带最深的那个像素明显比两边暗」。
        var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);

        var line = ForgeEditorSplitter.LineRect(splitter.Bounds.Size, isVertical: false);
        var onLine = splitter.TranslatePoint(line.Center, window)!.Value;
        var pane = PixelProbe.At(bitmap!, (int)onLine.X - 30, (int)onLine.Y);
        var darkest = Enumerable.Range(-3, 7)
            .Select(dx => PixelProbe.At(bitmap!, (int)onLine.X + dx, (int)onLine.Y))
            .MinBy(Luminance);

        Assert.True(
            Luminance(darkest) < Luminance(pane) - 2,
            $"分屏之间看不出分割线：这一带最深的是 {darkest}，两边底色是 {pane}");
    }

    [AvaloniaFact]
    public void 上下分屏的线是横着的()
    {
        var (window, area, vm) = Build();
        vm.Area.ArrangeGrid(["d0", "d1"], 2, 1);
        Render(window);

        var splitter = Assert.Single(area.GetVisualDescendants().OfType<ForgeEditorSplitter>());
        Assert.True(splitter.IsVertical);
        Assert.Equal(ForgeEditorSplitter.MouseZone, splitter.Bounds.Height, 3);
    }

    [AvaloniaFact]
    public void 拖分割线写回模型且重建后不回弹()
    {
        var (window, area, vm) = Build();
        vm.Area.ArrangeGrid(["d0", "d1"], 0, 0);
        Render(window);

        var split = Assert.IsType<EditorSplit>(vm.Area.Root);
        Assert.Equal(0.5f, split.Proportion, 3);

        var splitter = Assert.Single(area.GetVisualDescendants().OfType<ForgeEditorSplitter>());
        var grip = splitter.TranslatePoint(new Point(splitter.Bounds.Width / 2, splitter.Bounds.Height / 2), window)!.Value;

        // 往左拖到窗口的四分之一处
        var target = new Point(window.Width * 0.25, grip.Y);
        window.MouseDown(grip, MouseButton.Left);
        window.MouseMove(target);
        window.MouseUp(target, MouseButton.Left);
        Render(window);

        Assert.Equal(0.25f, split.Proportion, 2);

        // 重建（开一页再关掉）之后仍是拖出来的比例，不回到 0.5
        vm.Open("d2", "d2", new TextBlock());
        vm.Close("d2");
        Render(window);
        Assert.Equal(0.25f, split.Proportion, 2);
    }

    [AvaloniaFact]
    public void 比例夹在上下限之间()
    {
        var area = new EditorArea();
        area.Open(new EditorTab("d0", "d0"));
        area.Open(new EditorTab("d1", "d1"));
        area.ArrangeGrid(["d0", "d1"], 0, 0);

        var split = Assert.IsType<EditorSplit>(area.Root);

        Assert.True(area.SetProportion(split, 0f));
        Assert.Equal(EditorSplit.MinProportion, split.Proportion, 3);

        Assert.True(area.SetProportion(split, 5f));
        Assert.Equal(EditorSplit.MaxProportion, split.Proportion, 3);

        // 没变就不广播
        Assert.False(area.SetProportion(split, 5f));
    }

    /// <summary>感知亮度（Rec. 601）—— 只用来比"谁更深"。</summary>
    private static double Luminance(Color c) => (0.299 * c.R) + (0.587 * c.G) + (0.114 * c.B);

    private static (Window Window, ForgeEditorArea Area, EditorAreaViewModel Vm) Build()
    {
        var vm = new EditorAreaViewModel();
        var area = new ForgeEditorArea();
        var window = new Window { Content = area, Width = 800, Height = 600, WindowDecorations = WindowDecorations.None };
        area.Attach(vm);
        window.Show();

        vm.Open("d0", "文档 0", new TextBlock { Text = "0" });
        vm.Open("d1", "文档 1", new TextBlock { Text = "1" });
        Render(window);

        return (window, area, vm);
    }

    private static void Render(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
    }
}
