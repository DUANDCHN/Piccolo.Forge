using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
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
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());

        var frame = new ForgeToolWindowFrame
        {
            Palette = Palette,
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

        frame.Attach(manager);

        // 开局摆成 Rider 的样子：左边 Project、下边 Terminal
        manager.Activate("Project");
        manager.Activate("Terminal");

        return frame;
    }
}
