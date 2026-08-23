using CommunityToolkit.Mvvm.ComponentModel;

namespace Piccolo.Forge.Sample.Mvvm.ViewModels;

/// <summary>
/// 清单里的一条任务。普通的 <see cref="ObservableObject"/>，与 Forge 无关 ——
/// 面板 VM 里装的就是这类寻常对象。
/// </summary>
/// <param name="name">任务名。</param>
public sealed partial class TaskItemViewModel(string name) : ObservableObject
{
    /// <summary>任务名。</summary>
    public string Name { get; } = name;

    /// <summary>完成勾选。视图里 CheckBox 双向绑它；[ObservableProperty] 生成属性与变更通知。</summary>
    [ObservableProperty]
    private bool _isDone;
}
