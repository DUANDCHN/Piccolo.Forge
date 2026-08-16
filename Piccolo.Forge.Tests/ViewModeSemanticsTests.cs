using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 视图模式是 <c>type × isAutoHide</c> 的组合，不是五种 <see cref="ToolWindowType"/>。
/// </summary>
/// <remarks>
/// 判据在 <c>ToolWindowManagerLifecycle.kt:116</c>：
/// <c>if (!(windowInfo.isAutoHide || windowInfo.type == SLIDING)) return</c> ——
/// 失焦自动收起有**两个**来源。
/// <para>
/// 回归：曾经把 SLIDING 摆在「停靠不固定」那一格，于是既少了「取消停靠」，
/// 又让"停靠不固定"变成了"盖在编辑区上"，而真正的不固定（失焦收起）一直没有。
/// </para>
/// </remarks>
public class ViewModeSemanticsTests
{
    [Theory]
    [InlineData(ToolWindowViewMode.DockPinned, ToolWindowType.Docked, false)]
    [InlineData(ToolWindowViewMode.DockUnpinned, ToolWindowType.Docked, true)]
    [InlineData(ToolWindowViewMode.Undock, ToolWindowType.Sliding, false)]
    [InlineData(ToolWindowViewMode.Float, ToolWindowType.Floating, false)]
    [InlineData(ToolWindowViewMode.Window, ToolWindowType.Windowed, false)]
    public void 五种模式各自落到哪两个字段(ToolWindowViewMode mode, ToolWindowType type, bool autoHide)
    {
        var manager = Build();

        ToolWindowViewModes.Apply(manager, "相机列表", mode);

        var info = manager.Layout.GetInfo("相机列表")!;
        Assert.Equal(type, info.Type);
        Assert.Equal(autoHide, info.IsAutoHide);

        // 反向也要认得出来，否则菜单上的勾会打错
        Assert.Equal(mode, ToolWindowViewModes.Resolve(info));
    }

    /// <summary>停靠固定就是常驻：激活别的面板不该把它收掉。</summary>
    [Fact]
    public void 停靠固定不会因为别的面板被激活而收起()
    {
        var manager = Build();
        ToolWindowViewModes.Apply(manager, "相机列表", ToolWindowViewMode.DockPinned);
        manager.Show("相机列表");

        manager.Activate("图像库");

        Assert.True(manager.Layout.GetInfo("相机列表")!.IsVisible);
    }

    /// <summary>停靠不固定：占着位置，但焦点一走就收起。</summary>
    [Fact]
    public void 停靠不固定在别的面板被激活时收起()
    {
        var manager = Build();
        ToolWindowViewModes.Apply(manager, "相机列表", ToolWindowViewMode.DockUnpinned);
        manager.Show("相机列表");

        manager.Activate("图像库");

        Assert.False(manager.Layout.GetInfo("相机列表")!.IsVisible);

        // 形态没变 —— 收起来的是可见性，不是"被改成了别的模式"
        Assert.Equal(ToolWindowType.Docked, manager.Layout.GetInfo("相机列表")!.Type);
    }

    /// <summary>取消停靠（SLIDING）恒收起，与 isAutoHide 无关（判据里的另一半）。</summary>
    [Fact]
    public void 取消停靠也在失焦时收起()
    {
        var manager = Build();
        ToolWindowViewModes.Apply(manager, "相机列表", ToolWindowViewMode.Undock);
        manager.Show("相机列表");
        Assert.False(manager.Layout.GetInfo("相机列表")!.IsAutoHide);

        manager.Activate("图像库");

        Assert.False(manager.Layout.GetInfo("相机列表")!.IsVisible);
    }

    /// <summary>焦点回到编辑区，同样算失焦。</summary>
    [Fact]
    public void 编辑区拿到焦点时也收起()
    {
        var manager = Build();
        ToolWindowViewModes.Apply(manager, "相机列表", ToolWindowViewMode.DockUnpinned);
        manager.Show("相机列表");

        manager.OnEditorFocused();

        Assert.False(manager.Layout.GetInfo("相机列表")!.IsVisible);
    }

    /// <summary>自己被激活时当然不收自己。</summary>
    [Fact]
    public void 被激活的那个不收自己()
    {
        var manager = Build();
        ToolWindowViewModes.Apply(manager, "相机列表", ToolWindowViewMode.DockUnpinned);

        manager.Activate("相机列表");

        Assert.True(manager.Layout.GetInfo("相机列表")!.IsVisible);
    }

    private static ToolWindowManager Build()
    {
        var infos = new List<WindowInfo>
        {
            new() { Id = "相机列表", Anchor = ToolWindowAnchor.Left, Order = 0 },
            new() { Id = "图像库", Anchor = ToolWindowAnchor.Right, Order = 0 },
        };

        return new ToolWindowManager(new DesktopLayout(infos));
    }
}
