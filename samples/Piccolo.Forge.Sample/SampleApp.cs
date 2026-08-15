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

/// 示例：把默认布局（V2）的按钮按上游规则摆进左右两根条带。
internal sealed class SampleApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window
            {
                Title = "Piccolo.Forge — 默认布局落位",
                Width = 900,
                Height = 600,
                Content = BuildContent(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static Control BuildContent()
    {
        var layout = DefaultToolWindowLayout.CreateV2();

        var root = new DockPanel();
        root.Children.Add(Dock(BuildStripe(layout, StripeSide.Left), global::Avalonia.Controls.Dock.Left));
        root.Children.Add(Dock(BuildStripe(layout, StripeSide.Right), global::Avalonia.Controls.Dock.Right));
        root.Children.Add(new Border
        {
            Background = Brushes.Transparent,
            Child = new TextBlock
            {
                Text = "编辑区占位",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        });

        return root;
    }

    private static Control Dock(Control control, Dock dock)
    {
        DockPanel.SetDock(control, dock);
        return control;
    }

    private static Control BuildStripe(DesktopLayout layout, StripeSide side)
    {
        var stripe = new DockPanel { LastChildFill = false, Width = ForgeMetrics.StripeButtonSize };

        var top = BuildGroup(layout, side, StripeGroup.Top);
        DockPanel.SetDock(top, global::Avalonia.Controls.Dock.Top);
        stripe.Children.Add(top);

        var bottom = BuildGroup(layout, side, StripeGroup.Bottom);
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        stripe.Children.Add(bottom);

        return new Border
        {
            Child = stripe,
            BorderThickness = side == StripeSide.Left
                ? new Thickness(0, 0, 1, 0)
                : new Thickness(1, 0, 0, 0),
            BorderBrush = Brushes.Gray,
        };
    }

    private static Control BuildGroup(DesktopLayout layout, StripeSide side, StripeGroup group)
    {
        var anchor = group == StripeGroup.Bottom
            ? ToolWindowAnchor.Bottom
            : side == StripeSide.Left ? ToolWindowAnchor.Left : ToolWindowAnchor.Right;

        var windows = layout.GetSortedList()
            .Where(x => StripePlacementResolver.Resolve(x.Anchor, x.IsSplit) == new StripePlacement(side, group));

        // 分组分隔符只在新 UI 的 LEFT/RIGHT 竖条带里用（AbstractDroppableStripe.kt:99-105）
        var useSeparator = group == StripeGroup.Top;

        var panel = new StripeItemsPanel();
        foreach (var item in StripeGroupLayout.Build(windows, anchor, useSeparator))
        {
            panel.Children.Add(item.IsSeparator ? CreateSeparator() : CreateButton(item.Window!));
        }

        return panel;
    }

    private static Control CreateButton(WindowInfo info) => new Button
    {
        Width = ForgeMetrics.StripeButtonSize,
        Height = ForgeMetrics.StripeButtonSize,
        Content = info.Id[..1],
        CornerRadius = new CornerRadius(ForgeMetrics.StripeButtonArc),
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    // 32×11 占位，中间画一条 24×1 的线（StripeButtonSeparator.kt:22-37）
    private static Control CreateSeparator() => new Border
    {
        Width = ForgeMetrics.StripeSeparatorWidth,
        Height = ForgeMetrics.StripeSeparatorHeight,
        Child = new Border
        {
            Width = ForgeMetrics.StripeSeparatorLineWidth,
            Height = ForgeMetrics.StripeSeparatorLineHeight,
            Background = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };
}
