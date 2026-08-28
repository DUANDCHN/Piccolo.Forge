using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 把几条 <see cref="IDataTemplate"/> 并成一条：谁先匹配上用谁，都不匹配就当自己不存在。
/// 用在 <c>ContentTemplate</c>（**单值**属性）上，替代往控件的 <c>DataTemplates</c> 集合里塞模板。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么要有它 —— DataTemplates 会漏。</b>
/// 控件的 <c>DataTemplates</c> 是<b>沿逻辑树向上查</b>的：挂在 A 上的规则，A 的每一个后代
/// 在决定"我这份内容怎么画"时都会查到。于是给某个容器配一条兜底模板，等于替它内部
/// <b>所有</b>同类型内容做了决定 —— 包括宿主塞进来的那棵子树。
/// </para>
/// <para>
/// 通知卡就栽在这上面：卡片给"直接 Show 一个字符串"配了
/// <c>DataTemplate DataType="x:String"</c> → <c>SelectableTextBlock</c>，结果卡片里每一颗
/// <c>Content</c> 是文字的按钮都被接管 —— 按钮内容那层本该是 <c>AccessText</c>，成了可选中文本。
/// 后果不是长相而是<b>按钮点不动</b>：可选中文本为了框选自己吃掉 <c>PointerPressed</c>，
/// 按下事件到不了 <see cref="Button"/>。而悬停高亮是另一套机制、照常生效，
/// 于是症状是"背板会亮、点在字上没反应、点在内边距上才有反应"。
/// </para>
/// <para>
/// <c>ContentTemplate</c> 就没有这个问题：它是那一个呈现器自己的属性，不参与后代的查找。
/// 而框架取模板时是 <c>Match</c> 有条件地用它（<c>DataTemplateExtensions.FindDataTemplate</c>：
/// 先问 <c>ContentTemplate.Match</c>，不认才往上查），所以<b>不匹配就自动让路</b>给宿主的模板 ——
/// 这正是"卡片自带兜底、宿主仍可整个换掉"两件事同时成立的办法。
/// </para>
/// </remarks>
internal sealed class ForgeTemplateSet : IDataTemplate
{
    /// <summary>按书写顺序匹配的模板；XAML 里直接写成子元素。</summary>
    [Content]
    public DataTemplates Templates { get; } = [];

    /// <inheritdoc />
    public bool Match(object? data) => Templates.Any(t => t.Match(data));

    /// <inheritdoc />
    public Control? Build(object? data) => Templates.FirstOrDefault(t => t.Match(data))?.Build(data);
}
