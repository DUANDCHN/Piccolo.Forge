using Avalonia.Controls;

namespace Piccolo.Forge.Sample.Mvvm.Views;

/// <summary>任务面板的视图。逻辑全在 VM，这里只有装载。</summary>
public partial class TaskListPanelView : UserControl
{
    /// <summary>装载 XAML。</summary>
    public TaskListPanelView() => InitializeComponent();
}
