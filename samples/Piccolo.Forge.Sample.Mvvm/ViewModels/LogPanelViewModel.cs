using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Piccolo.Forge.Mvvm.CommunityToolkit.Controls;

namespace Piccolo.Forge.Sample.Mvvm.ViewModels;

/// <summary>
/// 日志面板：另一块工具窗口，演示两块面板经由构造注入通信（任务面板拿着它的 <see cref="Append"/>）。
/// </summary>
public sealed partial class LogPanelViewModel : ToolWindowPanel
{
    /// <summary>建日志面板。</summary>
    public LogPanelViewModel() : base("Log") => Title = "日志";

    /// <summary>日志行。</summary>
    public ObservableCollection<string> Lines { get; } = ["示例启动。"];

    /// <summary>追加一行。给别的面板当回调用。</summary>
    public void Append(string message) => Lines.Add($"{DateTime.Now:HH:mm:ss}  {message}");

    /// <summary>清空。</summary>
    [RelayCommand]
    private void Clear() => Lines.Clear();
}
