using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 两个标签都不许落回 Avalonia 自带的那份模板。
/// </summary>
/// <remarks>
/// ★ REQ-068 之后，"每个控件都有 ControlTheme"这条由
/// <see cref="ForgeThemeCompletenessTests"/> 守（它反射扫控件、两套主题都查），
/// 这里**不再维护那份手工清单** —— 两份清单必然有一份先过期，而过期的那份只会漏报。
/// 留在这里的是它测不到的另一件事：落回兜底模板。
/// </remarks>
public class ForgeThemeEntryTests
{
    /// <summary>
    /// 两个标签都不许落回 Avalonia 自带的那份模板。
    /// </summary>
    /// <remarks>
    /// ★ 这条是 <c>d7ee5f1</c> 那个回归的防回潮用例。
    /// <c>TabStripItem</c> 是 <c>ContentControl</c>，Avalonia 兜了一份带
    /// <c>PART_ContentPresenter</c> 的模板；而标签的容器由 <c>TabStrip</c> 建，
    /// <c>Content</c> 被喂成**视图模型对象本身** —— 一旦落回兜底模板，
    /// 它就把对象 <c>ToString()</c> 出来画在标签上
    /// （实机症状：标签行顶上多出一串 <c>Piccolo.Forge.Mvvm.EditorTabViewModel</c>）。
    /// <para>
    /// 成因是"清掉构造里的 <c>Template</c> 却没在 <c>ControlTheme</c> 里补一份"。
    /// 由此的规矩：**删构造里的 Template，必须同时补一份到主题里，哪怕是空的。**
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void 标签不会落回Avalonia自带的模板()
    {
        AssertNoFallbackTemplate(new ForgeEditorTab { Title = "文档" });
        AssertNoFallbackTemplate(new ForgeContentTab { Title = "内容" });
    }

    private static void AssertNoFallbackTemplate(ContentControl tab)
    {
        // 喂一个"不是字符串"的 Content —— 与 TabStrip 建容器时喂的东西同类
        tab.Content = new NotForDisplay();

        var window = new Window
        {
            Width = 300,
            Height = 60,
            Content = tab,
            RequestedThemeVariant = ThemeVariant.Dark,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(
            tab.GetVisualDescendants().OfType<ContentPresenter>(),
            x => x.Name == "PART_ContentPresenter");
        Assert.DoesNotContain(
            tab.GetVisualDescendants().OfType<TextBlock>(),
            x => x.Text == NotForDisplay.Marker);

        window.Close();
    }

    /// <summary>专门用来暴露"兜底模板把 Content 画出来了"的替身。</summary>
    private sealed class NotForDisplay
    {
        public const string Marker = "★这串不该出现在标签上★";

        public override string ToString() => Marker;
    }
}
