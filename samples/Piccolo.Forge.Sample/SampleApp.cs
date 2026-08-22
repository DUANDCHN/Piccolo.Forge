using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using System.Globalization;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Sample;

/// 示例：新 UI 的默认布局（V2），条带按钮可点开/收起；顶部一排开关演示明暗 / 语言 / 显示名字 / 角标。
/// 启动参数 <c>--islands</c> 换 Islands 主题（换主题要重启，所以做成启动参数而不是开关）。
internal sealed class SampleApp : Application
{
    /// <summary>命令行里有没有 <c>--islands</c>；由 Program 在 Initialize 之前设置。</summary>
    public static bool UseIslands { get; set; }

    private static ForgeThemeFamily Family => UseIslands ? ForgeThemeFamily.Islands : ForgeThemeFamily.ExpUi;

    private static ForgePalette Palette => ForgePalette.ForTheme(Family, Application.Current!.ActualThemeVariant);

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());

        // Forge 的主题：**唯一入口**，装的是哪族由这一句定。引用了哪个主题包就 new 哪个；
        // 不装控件就没有模板 —— 不报错，只是什么都不画。换族要重启（见 ForgeThemeManager）。
        IForgeTheme theme = UseIslands
            ? new Piccolo.Forge.Themes.Islands.ForgeIslandsTheme()
            : new Piccolo.Forge.Themes.ExpUi.ForgeExpUiTheme();
        ForgeThemeManager.Apply(this, theme);

        // 文案跟系统语言；也可以传 "zh-CN" / "ja-JP" 这样的名字
        ForgeThemeManager.ApplyLocale(this, CultureInfo.CurrentUICulture);
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window
            {
                Title = "Piccolo.Forge",
                Width = 1100,
                Height = 700,
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
        shell.Add(new SampleTool("Project", "Project", ForgeIcons.Project).Init(), ToolWindowAnchor.Left);
        shell.Add(new SampleTool("Commit", "Commit", ForgeIcons.Commit).Init(), ToolWindowAnchor.Left);
        shell.Add(new SampleTool("Structure", "Structure", ForgeIcons.Structure).Init(), ToolWindowAnchor.Left, isSplit: true);
        shell.Add(new SampleTool("Notifications", "Notifications", ForgeIcons.Notifications).Init(), ToolWindowAnchor.Right);
        shell.Add(new SampleTool("Database", "Database", ForgeIcons.Database).Init(), ToolWindowAnchor.Right);
        shell.Add(new SampleTool("Version Control", "Version Control", ForgeIcons.VersionControl).Init(), ToolWindowAnchor.Bottom);
        shell.Add(new SampleTool("Terminal", "Terminal", ForgeIcons.Terminal).Init(), ToolWindowAnchor.Bottom);
        shell.Add(new SampleTool("Services", "Services", ForgeIcons.Services).Init(), ToolWindowAnchor.Bottom, isSplit: true);

        // ③ 建控件并接上。配色跟着宿主的明暗变体走，这里不用管。
        var editor = new EditorAreaViewModel();
        editor.Open("sample-a", "Sample A", Document("Sample A"));
        editor.Open("sample-b", "Sample B", Document("Sample B"));
        editor.Area.DropTab("sample-b", editor.Area.Groups[0], EditorDropSide.Right);

        var editorControl = new ForgeEditorControl();
        editorControl.Attach(editor);

        var frame = new ForgeControl
        {
            EditorContent = editorControl,
            // 外观只在外框设一次，编辑区跟着继承；Family 与明暗正交
            Appearance = ForgeAppearance.Default with { Family = Family },
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

        var root = new DockPanel();
        var toolbar = BuildToolbar(shell);
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        root.Children.Add(frame);
        return root;
    }

    /// <summary>顶部一排开关：库能从外面拨动的几个维度都在这里，各自一句话。</summary>
    private static Control BuildToolbar(ToolWindowShellViewModel shell)
    {
        var dark = new CheckBox { Content = "Dark", IsChecked = true };
        dark.IsCheckedChanged += (_, _) =>
            Application.Current!.RequestedThemeVariant = dark.IsChecked == true ? ThemeVariant.Dark : ThemeVariant.Light;

        var names = new CheckBox { Content = "Show names" };
        names.IsCheckedChanged += (_, _) => shell.ShowToolWindowNames = names.IsChecked == true;

        var badge = new CheckBox { Content = "Badge on Notifications" };
        badge.IsCheckedChanged += (_, _) =>
        {
            var vm = shell.ToolWindows.First(w => w.Id == "Notifications");
            vm.HasBadge = badge.IsChecked == true;
        };

        var locale = new ComboBox
        {
            ItemsSource = ForgeThemeManager.SupportedLocales,
            SelectedItem = ForgeThemeManager.CurrentLocale,
            MinWidth = 110,
        };
        locale.SelectionChanged += (_, _) =>
        {
            if (locale.SelectedItem is string name)
            {
                ForgeThemeManager.ApplyLocale(Application.Current!, name);
            }
        };

        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Margin = new Thickness(12, 6),
        };
        bar.Children.Add(new TextBlock { Text = $"Theme: {Family} (restart with --islands / without to switch)", VerticalAlignment = VerticalAlignment.Center });
        bar.Children.Add(dark);
        bar.Children.Add(names);
        bar.Children.Add(badge);
        bar.Children.Add(new TextBlock { Text = "Locale", VerticalAlignment = VerticalAlignment.Center });
        bar.Children.Add(locale);
        return bar;
    }

    private static Control Document(string title) => new Border
    {
        Background = new SolidColorBrush(ForgeColor.Parse("#1E1F22")),
        Child = new TextBlock
        {
            Text = $"{title}\n\n把标签拖到另一个窗格中心合并",
            TextAlignment = TextAlignment.Center,
            Foreground = new SolidColorBrush(Palette.Foreground),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };
}
