using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 可观察层。
public class MvvmTests
{
    [Fact]
    public void The_mvvm_layer_takes_no_dependency_on_avalonia_or_any_mvvm_framework()
    {
        // 宿主用什么框架是宿主的事，控件库不该替它选（照 Bench 的规矩）
        var referenced = typeof(ToolWindowShellViewModel).Assembly
            .GetReferencedAssemblies()
            .Select(x => x.Name ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(referenced, x => x.StartsWith("Avalonia", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, x => x.StartsWith("CommunityToolkit", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, x => x.StartsWith("ReactiveUI", StringComparison.Ordinal));
        Assert.Contains("Piccolo.Forge.Model", referenced);
    }

    [Fact]
    public void Adding_a_tool_window_places_it_by_anchor_and_split_not_by_corner()
    {
        var shell = new ToolWindowShellViewModel();

        var vm = shell.Add("Terminal", "终端", ToolWindowAnchor.Bottom, isSplit: true);

        Assert.Equal(new StripePlacement(StripeSide.Right, StripeGroup.Bottom), vm.Placement);
        Assert.Same(vm, shell.Find("Terminal"));
    }

    [Fact]
    public void Opening_a_window_flips_IsOpen_and_raises_a_notification()
    {
        var shell = new ToolWindowShellViewModel();
        var vm = shell.Add("Project", "项目", ToolWindowAnchor.Left);

        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        shell.Open("Project");

        Assert.True(vm.IsOpen);
        Assert.True(vm.IsActive);
        Assert.Contains(nameof(ToolWindowViewModel.IsOpen), changed);
    }

    [Fact]
    public void The_slot_exclusion_rule_reaches_the_view_models()
    {
        // 「一格一个可见窗口」是模型层的规则，VM 只是跟着变
        var shell = new ToolWindowShellViewModel();
        var project = shell.Add("Project", "项目", ToolWindowAnchor.Left);
        var commit = shell.Add("Commit", "提交", ToolWindowAnchor.Left);

        shell.Open("Project");
        shell.Open("Commit");

        Assert.False(project.IsOpen);
        Assert.True(commit.IsOpen);
        Assert.Same(commit, shell.OpenOn(ToolWindowAnchor.Left));
    }

    [Fact]
    public void Moving_a_window_moves_it_between_stripe_groups_without_rebuilding_it()
    {
        var shell = new ToolWindowShellViewModel();
        var vm = shell.Add("Terminal", "终端", ToolWindowAnchor.Bottom);

        var leftBottom = shell.GetGroup(StripeSide.Left, StripeGroup.Bottom);
        var rightBottom = shell.GetGroup(StripeSide.Right, StripeGroup.Bottom);
        Assert.Contains(vm, leftBottom);

        shell.MoveTo("Terminal", ToolWindowAnchor.Bottom, isSplit: true);

        Assert.DoesNotContain(vm, shell.GetGroup(StripeSide.Left, StripeGroup.Bottom));
        Assert.Contains(vm, shell.GetGroup(StripeSide.Right, StripeGroup.Bottom));

        // 同一个实例挪过去的，不是重新造的
        Assert.Same(vm, shell.GetGroup(StripeSide.Right, StripeGroup.Bottom).Single());
    }

    [Fact]
    public void Toggling_one_window_does_not_disturb_another_windows_content()
    {
        // 这是整层存在的理由：开关面板不能把别人的内容重造一遍
        var shell = new ToolWindowShellViewModel();
        var project = shell.Add("Project", "项目", ToolWindowAnchor.Left);
        var terminal = shell.Add("Terminal", "终端", ToolWindowAnchor.Bottom);

        var liveContent = new object();
        project.Content = liveContent;
        shell.Open("Project");

        shell.Open("Terminal");
        shell.Close("Terminal");

        Assert.Same(liveContent, project.Content);
        Assert.Same(project, shell.Find("Project"));
    }

    [Fact]
    public void A_group_collection_only_moves_the_items_that_actually_moved()
    {
        // 整个清空再填会让绑定到它的控件全部重建
        var shell = new ToolWindowShellViewModel();
        shell.Add("A", "A", ToolWindowAnchor.Left);
        shell.Add("B", "B", ToolWindowAnchor.Left);

        var group = shell.GetGroup(StripeSide.Left, StripeGroup.Top);
        var events = new List<NotifyCollectionChangedAction>();
        ((INotifyCollectionChanged)group).CollectionChanged += (_, e) => events.Add(e.Action);

        shell.Open("A");

        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, events);
    }

    [Fact]
    public void Removing_the_stripe_button_takes_it_out_of_the_group()
    {
        var shell = new ToolWindowShellViewModel();
        var vm = shell.Add("Maven", "Maven", ToolWindowAnchor.Right);
        Assert.Contains(vm, shell.GetGroup(StripeSide.Right, StripeGroup.Top));

        shell.Close("Maven", removeFromStripe: true);

        Assert.False(vm.IsShowStripeButton);
        Assert.DoesNotContain(vm, shell.GetGroup(StripeSide.Right, StripeGroup.Top));
    }

    [Fact]
    public void Content_is_an_object_so_the_host_resolves_the_view()
    {
        // 控件库不认识宿主的视图，宿主塞 VM 进来即可
        var shell = new ToolWindowShellViewModel();
        var vm = shell.Add("Project", "项目", ToolWindowAnchor.Left);

        vm.Content = new { Whatever = 1 };

        Assert.NotNull(vm.Content);
        Assert.IsNotType<string>(vm.Content);
    }
}
