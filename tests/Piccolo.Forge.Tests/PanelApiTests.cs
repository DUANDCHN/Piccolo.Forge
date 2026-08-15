using CommunityToolkit.Mvvm.ComponentModel;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Mvvm.Controls;
using Xunit;
using CtPanels = Piccolo.Forge.Mvvm.CommunityToolkit.Controls;

namespace Piccolo.Forge.Tests;

/// 宿主接入的类型：继承一个 Tool，这个 VM 就是工具面板。
public partial class PanelApiTests
{
    /// 中立绑定：不用任何 MVVM 框架。
    private sealed class NeutralTool(string id) : ToolWindowPanel(id);

    /// CommunityToolkit 绑定：派生类里可以继续用 [ObservableProperty]。
    private sealed partial class ToolkitTool(string id) : CtPanels.ToolWindowPanel(id)
    {
        [ObservableProperty]
        public partial int Frames { get; set; }
    }

    private sealed class NeutralEditor(string id) : EditorPanel(id);

    [Fact]
    public void The_type_says_where_it_docks()
    {
        // 「不同类型的面板停靠方式不同」——由类型表达，不用宿主处理
        Assert.IsAssignableFrom<IToolWindowPanel>(new NeutralTool("a"));
        Assert.IsAssignableFrom<IEditorPanel>(new NeutralEditor("b"));

        // 编辑区面板停不到侧边：类型上就不成立
        Assert.IsNotAssignableFrom<IToolWindowPanel>(new NeutralEditor("b"));
    }

    [Fact]
    public void The_toolkit_binding_still_supports_observable_property()
    {
        // 这正是要分包的理由：[ObservableProperty] 要求继承 CommunityToolkit 的 ObservableObject
        var tool = new ToolkitTool("cam");
        var changed = new List<string?>();
        tool.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        tool.Frames = 42;

        Assert.Equal(42, tool.Frames);
        Assert.Contains(nameof(ToolkitTool.Frames), changed);
    }

    [Fact]
    public void Registering_a_panel_takes_its_identity_from_the_panel()
    {
        // 不再有两份 Title
        var shell = new ToolWindowShellViewModel();
        var panel = new NeutralTool("Project") { Title = "项目", Icon = "P" };

        var vm = shell.Add(panel, ToolWindowAnchor.Left);

        Assert.Equal("Project", vm.Id);
        Assert.Equal("项目", vm.Title);
        Assert.Equal("P", vm.Icon);
        Assert.Same(panel, vm.Content);
    }

    [Fact]
    public void Renaming_the_panel_reaches_the_shell()
    {
        // 切语言只改一处
        var shell = new ToolWindowShellViewModel();
        var panel = new NeutralTool("Project") { Title = "项目" };
        var vm = shell.Add(panel, ToolWindowAnchor.Left);

        panel.Title = "Project";
        panel.Icon = "★";

        Assert.Equal("Project", vm.Title);
        Assert.Equal("★", vm.Icon);
    }

    [Fact]
    public void The_toolkit_panel_also_flows_its_title_through()
    {
        var shell = new ToolWindowShellViewModel();
        var panel = new ToolkitTool("Cam") { Title = "相机" };
        var vm = shell.Add(panel, ToolWindowAnchor.Right);

        panel.Title = "Camera";

        Assert.Equal("Camera", vm.Title);
    }

    [Fact]
    public void Registering_a_panel_needs_only_the_anchor_and_the_half()
    {
        // 按钮画在哪根条带是算出来的，宿主不用说
        var shell = new ToolWindowShellViewModel();

        var left = shell.Add(new NeutralTool("A"), ToolWindowAnchor.Bottom);
        var right = shell.Add(new NeutralTool("B"), ToolWindowAnchor.Bottom, isSplit: true);

        Assert.Equal(new StripePlacement(StripeSide.Left, StripeGroup.Bottom), left.Placement);
        Assert.Equal(new StripePlacement(StripeSide.Right, StripeGroup.Bottom), right.Placement);
    }

    [Fact]
    public void The_neutral_packages_stay_framework_free()
    {
        // 接口在中立包、基类按框架分包（照 Dock：Dock.Model 无框架，Dock.Model.Mvvm 才引 CommunityToolkit）
        foreach (var assembly in new[] { typeof(IToolWindowPanel).Assembly, typeof(ToolWindowPanel).Assembly })
        {
            var referenced = assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty);
            Assert.DoesNotContain(referenced, x => x.StartsWith("CommunityToolkit", StringComparison.Ordinal));
            Assert.DoesNotContain(referenced, x => x.StartsWith("Avalonia", StringComparison.Ordinal));
        }

        // 而 CommunityToolkit 绑定包正是允许引它的那一个
        Assert.Contains(
            typeof(CtPanels.ToolWindowPanel).Assembly.GetReferencedAssemblies().Select(x => x.Name),
            x => x == "CommunityToolkit.Mvvm");
    }
}
