using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Sample;

/// 示例：新 UI 的默认布局（V2），条带按钮可点开/收起。
internal sealed class SampleApp : Application
{
    private static readonly ForgePalette Palette = ForgePalette.ExpUiDark;

    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window
            {
                Title = "Piccolo.Forge",
                Width = 1100,
                Height = 700,
                Background = new SolidColorBrush(Palette.Background),
                Content = BuildContent(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static Control BuildContent()
    {
        // ① 建外壳 VM —— 宿主的接入点
        var shell = new ToolWindowShellViewModel();

        // ② 登记面板：把面板本身交出去，身份从它自己来。
        //    参数里只剩「停哪条边、哪一半」——按钮画在哪根条带是算出来的。
        shell.Add(new SampleTool("Project", "Project", "P").Init(), ToolWindowAnchor.Left);
        shell.Add(new SampleTool("Commit", "Commit", "C").Init(), ToolWindowAnchor.Left);
        shell.Add(new SampleTool("Structure", "Structure", "S").Init(), ToolWindowAnchor.Left, isSplit: true);
        shell.Add(new SampleTool("Notifications", "Notifications", "N").Init(), ToolWindowAnchor.Right);
        shell.Add(new SampleTool("Database", "Database", "D").Init(), ToolWindowAnchor.Right);
        shell.Add(new SampleTool("Version Control", "Version Control", "V").Init(), ToolWindowAnchor.Bottom);
        shell.Add(new SampleTool("Terminal", "Terminal", "T").Init(), ToolWindowAnchor.Bottom);
        shell.Add(new SampleTool("Services", "Services", "E").Init(), ToolWindowAnchor.Bottom, isSplit: true);

        // ③ 建控件并接上。配色跟着宿主的明暗变体走，这里不用管。
        var frame = new ForgeToolWindowFrame
        {
            EditorContent = new Border
            {
                Background = new SolidColorBrush(ForgeColor.Parse("#1E1F22")),
                Child = new TextBlock
                {
                    Text = "编辑区占位\n\n点左右条带上的按钮开合工具窗口",
                    TextAlignment = TextAlignment.Center,
                    Foreground = new SolidColorBrush(Palette.Foreground),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            },
        };

        frame.Attach(shell);

        // ④ 视图由**宿主**解析。真实应用里这一步交给 ViewLocator / DataTemplate；
        //    示例简单，直接给控件。
        foreach (var vm in shell.ToolWindows)
        {
            if (vm.Content is SampleTool tool)
            {
                vm.Content = new TextBlock
                {
                    Text = tool.Body,
                    Margin = new Thickness(12, 8, 0, 0),
                    Foreground = new SolidColorBrush(Palette.Foreground),
                };
            }
        }

        // 开局摆成 Rider 的样子
        shell.Open("Project");
        shell.Open("Structure");
        shell.Open("Version Control");

        return frame;
    }
}
