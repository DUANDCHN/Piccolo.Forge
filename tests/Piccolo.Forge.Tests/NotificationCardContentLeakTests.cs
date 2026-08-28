using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 通知卡自带的两条兜底模板（<c>INotification</c> / 字符串）**只能管卡片自己那份内容**，
/// 不许漏到宿主塞进来的子树里。
/// </summary>
/// <remarks>
/// <para>
/// 病根：这两条原先挂在 <c>PART_Content</c> 的 <c>DataTemplates</c> 集合上，而
/// <c>DataTemplates</c> 是<b>沿逻辑树向上查</b>的 —— 于是卡片里每一颗 <c>Content</c> 是文字的
/// 按钮都被字符串那条接管，内容层从 <c>AccessText</c> 变成 <c>SelectableTextBlock</c>。
/// 后果不是长相而是<b>按钮点不动</b>：可选中文本为了框选自己吃掉 <c>PointerPressed</c>。
/// </para>
/// <para>
/// 症状极具迷惑性 —— 悬停高亮照常（那是另一套机制），点在字上没反应、点在内边距上才有反应。
/// 所以下面那条用例<b>两个落点各点一次</b>，且直接断言"画字那层不是可选中文本"：
/// 只点内边距的话，漏回来了也照样绿。
/// </para>
/// <para>解法见 <c>Extras.axaml</c>：改挂 <c>ContentTemplate</c>（<see cref="ForgeTemplateSet"/>）。</para>
/// </remarks>
public class NotificationCardContentLeakTests
{
    /// <summary>宿主自己的通知内容：一段标题 + 一颗文字按钮。</summary>
    private sealed class HostContent
    {
        public string Title => "已保存";
        public int Clicks { get; private set; }
        public void Click() => Clicks++;
    }

    private static (Window window, ForgeNotificationManager manager) Host(IDataTemplate? hostTemplate = null)
    {
        var manager = new ForgeNotificationManager();
        if (hostTemplate is not null)
        {
            manager.DataTemplates.Add(hostTemplate);
        }

        var window = new Window { Width = 600, Height = 400, Content = manager };
        window.Show();
        Settle(window);
        return (window, manager);
    }

    private static void Settle(Window window)
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(20);
        }
    }

    [AvaloniaFact]
    public void 宿主内容里的文字按钮_点在字上也能点动()
    {
        var content = new HostContent();
        var template = new FuncDataTemplate<HostContent>((vm, _) =>
        {
            var button = new Button { Content = "打开所在目录" };
            button.Click += (_, _) => vm!.Click();
            return new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = vm!.Title },
                    button,
                },
            };
        });

        var (window, manager) = Host(template);
        manager.Show(content, NotificationType.Information, TimeSpan.Zero);
        Settle(window);

        var button = window.GetVisualDescendants().OfType<Button>()
                           .Single(b => b.Content is "打开所在目录");

        // ① 病根本身：画字那一层是可聚焦的选择型文本，就会吞掉 PointerPressed
        var painted = button.GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.IsNotType<SelectableTextBlock>(painted);
        Assert.False(painted.Focusable);

        // ② 文字正中 与 左侧内边距 各点一次 —— 只测后者的话，漏回来了也照样绿
        ClickAt(window, button, painted.TranslatePoint(
            new Point(painted.Bounds.Width / 2, painted.Bounds.Height / 2), window)!.Value);
        Assert.Equal(1, content.Clicks);

        ClickAt(window, button, button.TranslatePoint(
            new Point(2, button.Bounds.Height / 2), window)!.Value);
        Assert.Equal(2, content.Clicks);

        window.Close();

        static void ClickAt(Window window, Button button, Point point)
        {
            window.MouseMove(point);
            Dispatcher.UIThread.RunJobs();
            Assert.True(button.IsPointerOver, "落点没落在按钮上，用例本身失效");
            window.MouseDown(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void 直接显示字符串_仍走卡片自带的可选中文本()
    {
        var (window, manager) = Host();
        manager.Show("一段正文", NotificationType.Information, TimeSpan.Zero);
        Settle(window);

        var card = window.GetVisualDescendants().OfType<NotificationCard>().Single();
        var text = card.GetVisualDescendants().OfType<SelectableTextBlock>().Single();
        Assert.Equal("一段正文", text.Text);

        window.Close();
    }

    [AvaloniaFact]
    public void 显示Notification对象_标题与正文都画出来()
    {
        var (window, manager) = Host();
        manager.Show(new Notification("标题", "正文"), NotificationType.Information, TimeSpan.Zero);
        Settle(window);

        var card = window.GetVisualDescendants().OfType<NotificationCard>().Single();
        var texts = card.GetVisualDescendants().OfType<SelectableTextBlock>()
                        .Select(t => t.Text).ToList();
        Assert.Contains("标题", texts);
        Assert.Contains("正文", texts);

        window.Close();
    }
}
