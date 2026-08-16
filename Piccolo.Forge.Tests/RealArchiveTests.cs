using System.Xml.Linq;
using Avalonia.Controls;
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
/// 回归：那份存档里 <c>CamList</c> 是 <c>type="SLIDING"</c>、<c>ImageLibrary</c> 是默认的 DOCKED，
/// 而 <c>ToolWindowSlot</c> 的键**含形态**（<c>ToolWindowManagerImpl.kt:962-966</c>），
/// 所以模型认为两者不同格、都可见是合法的 —— 上游确实如此，SLIDING 是滑出覆盖在编辑区上、不占列。
/// 但 Forge 还没实现 SLIDING，视图把它当普通停靠面板塞进同一列，两块叠着画：
/// 「两个按钮都亮着、面板只显示一个，关掉一个另一个才冒出来」。
/// <para>
/// 病根是**菜单摆出了库画不出来的形态**（视图模式 → 停靠不固定）。现在菜单只列
/// <c>ForgeToolWindowFrame.SupportedViewModes</c>，并在接上 / 读档时把超纲的形态掰回 DOCKED ——
/// 后者是为了治好已经躺在用户磁盘上的那些存档。
/// </para>
/// </remarks>
public class RealArchiveTests
{
    // 用户磁盘上那份（AppSettings.json → ShellLayoutStore.ToolWindows），原样照抄
    private const string Archive =
        """
        <layout><window_info id="CamList" type="SLIDING" internal_type="SLIDING" visible="true" weight="0.19611597" order="0" /><window_info id="ImageLibrary" visible="true" weight="0.19611597" order="1" /><window_info id="LogView" anchor="bottom" visible="true" weight="0.35845986" sideWeight="0.5463776" order="0" /><window_info id="ImageStats" anchor="bottom" weight="0.35845986" sideWeight="0.50738513" order="1" /><window_info id="MeasureTable" anchor="bottom" weight="0.45824295" order="2" /><window_info id="Histogram" anchor="bottom" side_tool="true" weight="0.45824295" sideWeight="0.46252736" order="3" /><window_info id="ProfileLine" anchor="bottom" side_tool="true" weight="0.35845986" sideWeight="0.50738513" order="4" /><window_info id="PixelInspector" anchor="bottom" side_tool="true" weight="0.45824295" order="5" /><window_info id="FocusMeasure" anchor="bottom" side_tool="true" weight="0.45824295" sideWeight="0.5463776" order="6" /><window_info id="CamParam" anchor="right" visible="true" weight="0.25410286" order="0" /><unified_weights top="0.33" left="0.19611597" bottom="0.35845986" right="0.25410286" /></layout>
        """;

    [AvaloniaFact]
    public void 读到画不出来的形态就掰回停靠固定()
    {
        // 宿主是先登记面板、后读存档（MainWindowViewModel.SetupShell），这里照做
        var shell = new ToolWindowShellViewModel();
        foreach (var id in new[]
                 {
                     "CamList", "ImageLibrary", "LogView", "ImageStats", "MeasureTable",
                     "Histogram", "ProfileLine", "PixelInspector", "FocusMeasure", "CamParam",
                 })
        {
            shell.Add(id, id, ToolWindowAnchor.Left, () => new object());
        }

        // 宿主真实顺序：控件先接上，再读存档（MainWindowViewModel.SetupShell）
        var frame = new ForgeToolWindowFrame();
        frame.Attach(shell);
        shell.RestoreLayout(XElement.Parse(Archive));

        var visible = shell.Manager.Layout.GetSortedList()
            .Where(x => x.IsVisible)
            .Select(x => $"{x.Id}({x.Anchor}{(x.IsSplit ? ",split" : string.Empty)})");

        // 左边只剩一个开着（原先 CamList 与 ImageLibrary 都开着，因为形态不同 = 不同格）
        Assert.Equal(["CamList(Left)", "LogView(Bottom)", "CamParam(Right)"], visible);

        // 超纲的形态被掰回来了，不会再有第二种形态占第二个格
        Assert.All(
            shell.Manager.Layout.GetSortedList(),
            x => Assert.Contains(x.Type, ForgeToolWindowFrame.SupportedViewModes));
    }
}
