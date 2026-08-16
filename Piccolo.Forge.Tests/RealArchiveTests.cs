using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.Headless.XUnit;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 拿**用户磁盘上那份真实存档**跑一遍（AppSettings.json → ShellLayoutStore.ToolWindows）。
/// </summary>
/// <remarks>
/// 那份存档里 <c>CamList</c> 是 <c>type="SLIDING"</c>、<c>ImageLibrary</c> 是默认的 DOCKED，
/// 而 <c>ToolWindowSlot</c> 的键**含形态**（<c>ToolWindowManagerImpl.kt:962-966</c>），
/// 所以两者不同格、都可见 —— 上游确实如此：SLIDING 是滑出**覆盖在编辑区上**、不占列。
/// <para>
/// 曾经的 bug 不在模型而在视图：Forge 没实现 SLIDING，把它当普通停靠面板塞进同一列，
/// 两块叠着画（「两个按钮都亮着、只看得到一个」）。现在 SLIDING 走覆盖层，这条钉的是
/// **同一份存档读进来之后，编辑区不该被那块覆盖层挤掉**。
/// </para>
/// </remarks>
public class RealArchiveTests
{
    private const string Archive =
        """
        <layout><window_info id="CamList" type="SLIDING" internal_type="SLIDING" visible="true" weight="0.19611597" order="0" /><window_info id="ImageLibrary" visible="true" weight="0.19611597" order="1" /><window_info id="LogView" anchor="bottom" visible="true" weight="0.35845986" sideWeight="0.5463776" order="0" /><window_info id="ImageStats" anchor="bottom" weight="0.35845986" sideWeight="0.50738513" order="1" /><window_info id="MeasureTable" anchor="bottom" weight="0.45824295" order="2" /><window_info id="Histogram" anchor="bottom" side_tool="true" weight="0.45824295" sideWeight="0.46252736" order="3" /><window_info id="ProfileLine" anchor="bottom" side_tool="true" weight="0.35845986" sideWeight="0.50738513" order="4" /><window_info id="PixelInspector" anchor="bottom" side_tool="true" weight="0.45824295" order="5" /><window_info id="FocusMeasure" anchor="bottom" side_tool="true" weight="0.45824295" sideWeight="0.5463776" order="6" /><window_info id="CamParam" anchor="right" visible="true" weight="0.25410286" order="0" /><unified_weights top="0.33" left="0.19611597" bottom="0.35845986" right="0.25410286" /></layout>
        """;

    [AvaloniaFact]
    public void 滑出的那块盖在编辑区上而不是挤掉它()
    {
        var shell = new ToolWindowShellViewModel();
        foreach (var id in new[]
                 {
                     "CamList", "ImageLibrary", "LogView", "ImageStats", "MeasureTable",
                     "Histogram", "ProfileLine", "PixelInspector", "FocusMeasure", "CamParam",
                 })
        {
            shell.Add(id, id, ToolWindowAnchor.Left, () => new object());
        }

        var editor = new Border();
        var frame = new ForgeToolWindowFrame { EditorContent = editor };
        frame.Attach(shell);

        // 宿主真实顺序：控件先接上，再读存档（MainWindowViewModel.SetupShell）
        shell.RestoreLayout(XElement.Parse(Archive));

        var window = new Window { Width = 1000, Height = 600, WindowDecorations = WindowDecorations.None, Content = frame };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();

        // 形态原样保留（不再被掰回停靠固定）
        Assert.Equal(ToolWindowType.Sliding, shell.Manager.Layout.GetInfo("CamList")!.Type);
        Assert.Equal(ToolWindowType.Docked, shell.Manager.Layout.GetInfo("ImageLibrary")!.Type);

        // 左边那一列的宽度只由**停靠的那个**决定；滑出的那块盖在编辑区上、不占地方。
        // 基准是停靠区自己的宽度（窗口宽减掉左右两条条带），不是窗口宽。
        var left = shell.Manager.Layout.GetInfo("ImageLibrary")!.Weight * frame.DockPane.Bounds.Width;
        Assert.Equal(left, editor.Bounds.X, 1.0);

        // 两块面板不再叠在同一个矩形里
        var docked = frame.DockPane.Children.OfType<Border>().Where(x => x.Bounds.Width > 0 && x != editor).ToList();
        Assert.Equal(docked.Count, docked.Select(x => x.Bounds).Distinct().Count());
    }
}
