using Avalonia.Media;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Mvvm.Controls;

namespace Piccolo.Forge.Sample;

/// <summary>
/// 一块示例工具面板。
/// </summary>
/// <remarks>
/// <b>继承 <see cref="ToolWindowPanel"/> 就够了</b> —— 这个 VM 于是成为一块
/// 停在边上的工具窗口。停哪条边登记时说一次；按钮画在哪根条带是算出来的。
/// <para>
/// 这里用的是**中立绑定**（<c>Piccolo.Forge.Mvvm</c>）。宿主若要在派生类里用
/// <c>[ObservableProperty]</c>，改用 <c>Piccolo.Forge.Mvvm.CommunityToolkit</c>
/// 里同名的那套。
/// </para>
/// </remarks>
/// <param name="id">面板 id。</param>
/// <param name="title">显示名。</param>
/// <param name="glyph">条带按钮上的图标。</param>
internal sealed class SampleTool(string id, string title, Geometry glyph) : ToolWindowPanel(id)
{
    /// <summary>面板里显示的正文。</summary>
    public string Body { get; } = $"{title} 的内容";

    /// <summary>把标题与图标一次性摆好。</summary>
    public SampleTool Init()
    {
        Title = title;
        Icon = glyph;
        return this;
    }
}
