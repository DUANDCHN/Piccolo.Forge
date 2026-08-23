using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Sample.Shared;

/// <summary>
/// 外壳（停靠 / 工具窗 / 编辑器标签）的演示内容，两档共用。
/// </summary>
/// <remarks>
/// ⚠ 本工程**不引任何主题包**：装哪一族由各示例壳自己决定（各引一个包），
/// 这里只接收一个 <see cref="ForgeThemeFamily"/> 参数用于外框的 Appearance 与取色。
/// </remarks>
public static class ShellDemo
{
    static ForgePalette Pal(ForgeThemeFamily family) =>
        ForgePalette.ForTheme(family, Application.Current!.ActualThemeVariant);

    public static Control Build(ForgeThemeFamily family)
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
        editor.Open("sample-a", "Sample A", Document(family, "Sample A"));
        editor.Open("sample-b", "Sample B", Document(family, "Sample B"));
        editor.Area.DropTab("sample-b", editor.Area.Groups[0], EditorDropSide.Right);

        var editorControl = new ForgeEditorControl();
        editorControl.Attach(editor);

        var frame = new ForgeControl
        {
            EditorContent = editorControl,
            // 外观只在外框设一次，编辑区跟着继承；Family 与明暗正交
            Appearance = ForgeAppearance.Default with { Family = family },
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
                    Foreground = new SolidColorBrush(Pal(family).Foreground),
                };
            }
        }

        // 开局摆成 Rider 的样子
        shell.Open("Project");
        shell.Open("Structure");
        shell.Open("Version Control");

        var root = new DockPanel();
        var toolbar = BuildToolbar(shell, family);
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        root.Children.Add(frame);
        return root;
    }

    /// <summary>顶部一排开关：库能从外面拨动的几个维度都在这里，各自一句话。</summary>
    static Control BuildToolbar(ToolWindowShellViewModel shell, ForgeThemeFamily family)
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
        bar.Children.Add(new TextBlock { Text = $"Theme: {family}", VerticalAlignment = VerticalAlignment.Center });
        bar.Children.Add(dark);
        bar.Children.Add(names);
        bar.Children.Add(badge);
        bar.Children.Add(new TextBlock { Text = "Locale", VerticalAlignment = VerticalAlignment.Center });
        bar.Children.Add(locale);
        return bar;
    }

    static Control Document(ForgeThemeFamily family, string title) => new Border
    {
        Background = new SolidColorBrush(ForgeColor.Parse("#1E1F22")),
        Child = new TextBlock
        {
            Text = $"{title}\n\n把标签拖到另一个窗格中心合并",
            TextAlignment = TextAlignment.Center,
            Foreground = new SolidColorBrush(Pal(family).Foreground),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };
}
