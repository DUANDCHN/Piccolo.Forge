using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Piccolo.Forge.Mvvm.CommunityToolkit.Controls;

namespace Piccolo.Forge.Sample.Mvvm.ViewModels;

/// <summary>
/// 任务清单面板。**继承 <see cref="ToolWindowPanel"/>，这个 VM 就是一块停在边上的工具窗口。**
/// </summary>
/// <remarks>
/// 类里只有状态与命令，没有一行控件代码 —— 长相在 <c>Views/TaskListPanelView.axaml</c>，
/// 由 <see cref="ViewLocator"/> 按名字解析。停哪条边是登记时的事（见 MainViewModel），不在面板上。
/// </remarks>
public sealed partial class TaskListPanelViewModel : ToolWindowPanel
{
    private readonly Action<string> _log;
    private int _next = 4;

    /// <summary>建面板。身份（Id / Title）在这里给。</summary>
    /// <param name="log">跨面板通信走普通依赖注入 —— 这里是日志面板递进来的回调。</param>
    public TaskListPanelViewModel(Action<string> log) : base("Tasks")
    {
        _log = log;
        Title = "任务";
        Items =
        [
            new TaskItemViewModel("接入 Forge 外壳"),
            new TaskItemViewModel("面板改成 VM"),
            new TaskItemViewModel("视图交给 ViewLocator"),
        ];
    }

    /// <summary>任务集合。集合变更自动反映到列表 —— ObservableCollection 的本职。</summary>
    public ObservableCollection<TaskItemViewModel> Items { get; }

    /// <summary>选中项。[ObservableProperty] 生成 SelectedItem 属性与变更通知。</summary>
    [ObservableProperty]
    private TaskItemViewModel? _selectedItem;

    /// <summary>[RelayCommand] 生成 AddTaskCommand，视图里按钮直接绑它。</summary>
    [RelayCommand]
    private void AddTask()
    {
        var item = new TaskItemViewModel($"新任务 {_next++}");
        Items.Add(item);
        SelectedItem = item;
        _log($"添加：{item.Name}");
    }

    /// <summary>带 CanExecute 的命令：没选中时按钮自动置灰（通知由下面的 partial 方法接好）。</summary>
    [RelayCommand(CanExecute = nameof(CanRemoveSelected))]
    private void RemoveSelected()
    {
        _log($"删除：{SelectedItem!.Name}");
        Items.Remove(SelectedItem!);
        SelectedItem = null;
    }

    private bool CanRemoveSelected() => SelectedItem is not null;

    /// <summary>选中变了要主动捅一下 CanExecute —— 生成器只生成命令，不猜依赖关系。</summary>
    partial void OnSelectedItemChanged(TaskItemViewModel? value) =>
        RemoveSelectedCommand.NotifyCanExecuteChanged();
}
