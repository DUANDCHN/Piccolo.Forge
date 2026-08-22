using System.Xml.Linq;
using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 存档的形状与属性名。
public class ToolWindowLayoutSerializerTests
{
    [Fact]
    public void Tags_match_upstream()
    {
        // DesktopLayout.kt:25、WindowInfoImpl.kt:34、UnifiedToolWindowWeights.kt:17
        Assert.Equal("layout", ToolWindowLayoutSerializer.LayoutTag);
        Assert.Equal("window_info", ToolWindowLayoutSerializer.WindowInfoTag);
        Assert.Equal("unified_weights", ToolWindowLayoutSerializer.UnifiedWeightsTag);
    }

    [Fact]
    public void Only_non_default_attributes_are_written()
    {
        // 上游用 BaseState 的 property 委托，等于默认值就不进 XML
        var layout = new DesktopLayout([new WindowInfo { Id = "X" }]);

        var element = ToolWindowLayoutSerializer.Write(layout).Elements("window_info").Single();

        Assert.Equal(["id"], element.Attributes().Select(x => x.Name.LocalName));
    }

    [Fact]
    public void Attribute_names_match_the_audited_table()
    {
        // 工具窗口模型.md §2 那张表
        var layout = new DesktopLayout([
            new WindowInfo
            {
                Id = "X",
                Anchor = ToolWindowAnchor.Bottom,
                IsSplit = true,
                Type = ToolWindowType.Sliding,
                ContentUiType = ToolWindowContentUiType.Combo,
                IsVisible = true,
                IsAutoHide = true,
                IsShowStripeButton = false,
                Weight = 0.4f,
                SideWeight = 0.25f,
                Order = 3,
            },
        ]);

        var element = ToolWindowLayoutSerializer.Write(layout).Elements("window_info").Single();

        Assert.Equal("bottom", (string?)element.Attribute("anchor"));
        Assert.Equal("true", (string?)element.Attribute("side_tool"));
        Assert.Equal("SLIDING", (string?)element.Attribute("type"));
        Assert.Equal("combo", (string?)element.Attribute("content_ui"));
        Assert.Equal("true", (string?)element.Attribute("visible"));
        Assert.Equal("true", (string?)element.Attribute("auto_hide"));
        Assert.Equal("false", (string?)element.Attribute("show_stripe_button"));
        Assert.Equal("0.4", (string?)element.Attribute("weight"));
        Assert.Equal("0.25", (string?)element.Attribute("sideWeight"));
        Assert.Equal("3", (string?)element.Attribute("order"));
    }

    [Fact]
    public void Floating_bounds_are_flattened_into_four_attributes()
    {
        // WindowInfoImpl.kt:52-53 —— @Property(flat = true)
        var layout = new DesktopLayout([
            new WindowInfo { Id = "X", FloatingBounds = new FloatingBounds(10, 20, 300, 400) },
        ]);

        var element = ToolWindowLayoutSerializer.Write(layout).Elements("window_info").Single();

        Assert.Equal("10", (string?)element.Attribute("x"));
        Assert.Equal("400", (string?)element.Attribute("height"));
    }

    [Fact]
    public void All_zero_bounds_read_back_as_undefined()
    {
        // WindowInfoImpl.kt:53 —— 全零等于 null
        var element = XElement.Parse(
            """<layout><window_info id="X" x="0" y="0" width="0" height="0" /></layout>""");

        Assert.Null(ToolWindowLayoutSerializer.Read(element).GetInfo("X")!.FloatingBounds);
    }

    [Fact]
    public void Entries_without_an_id_are_dropped()
    {
        // DesktopLayout.kt:113-116
        var element = XElement.Parse("""<layout><window_info anchor="left" /><window_info id="X" /></layout>""");

        Assert.Single(ToolWindowLayoutSerializer.Read(element).GetSortedList());
    }

    [Fact]
    public void Reading_normalizes_the_order()
    {
        // DesktopLayout.kt:127 —— readExternal 末尾调 normalizeOrder
        var element = XElement.Parse(
            """
            <layout>
              <window_info id="A" anchor="left" order="7" />
              <window_info id="B" anchor="left" order="9" />
            </layout>
            """);

        var layout = ToolWindowLayoutSerializer.Read(element);

        Assert.Equal(0, layout.GetInfo("A")!.Order);
        Assert.Equal(1, layout.GetInfo("B")!.Order);
    }

    [Fact]
    public void Unknown_anchor_falls_back_to_left()
    {
        // WindowInfoImpl.kt:149-163 · ToolWindowAnchorConverter
        var element = XElement.Parse("""<layout><window_info id="X" anchor="nowhere" /></layout>""");

        Assert.Equal(ToolWindowAnchor.Left, ToolWindowLayoutSerializer.Read(element).GetInfo("X")!.Anchor);
    }

    [Fact]
    public void Unified_weights_survive_a_round_trip()
    {
        var layout = DefaultToolWindowLayout.CreateV2();
        layout.SetUnifiedAnchorWeight(ToolWindowAnchor.Left, 0.4f);

        var reloaded = ToolWindowLayoutSerializer.Read(ToolWindowLayoutSerializer.Write(layout));

        Assert.Equal(0.4f, reloaded.GetUnifiedAnchorWeight(ToolWindowAnchor.Left));
        Assert.Equal(WindowInfo.DefaultWeight, reloaded.GetUnifiedAnchorWeight(ToolWindowAnchor.Right));
    }

    [Fact]
    public void The_default_layout_survives_a_round_trip()
    {
        var original = DefaultToolWindowLayout.CreateV2();

        var reloaded = ToolWindowLayoutSerializer.Read(ToolWindowLayoutSerializer.Write(original));

        Assert.Equal(
            original.GetSortedList().Select(x => (x.Id, x.Anchor, x.Order, x.Weight, x.IsSplit, x.ContentUiType)),
            reloaded.GetSortedList().Select(x => (x.Id, x.Anchor, x.Order, x.Weight, x.IsSplit, x.ContentUiType)));
    }

    // ── 标题条上那排内容 tab 的顺序（用户拖出来的）────────────────────────

    [Fact]
    public void 内容tab顺序_原样往返()
    {
        var layout = new DesktopLayout([new WindowInfo { Id = "camParam", ContentOrder = ["tree", "preprocess", "common"] }]);

        var back = ToolWindowLayoutSerializer.Read(ToolWindowLayoutSerializer.Write(layout));

        Assert.Equal(["tree", "preprocess", "common"], back.GetInfo("camParam")!.ContentOrder);
    }

    [Fact]
    public void 没有内容tab的面板不写这一项()
    {
        // 绝大多数面板没有内容 tab，存档里不该因此多一个属性
        var layout = new DesktopLayout([new WindowInfo { Id = "X" }]);

        var element = ToolWindowLayoutSerializer.Write(layout).Elements("window_info").Single();

        Assert.Null(element.Attribute("content_order"));
    }

    [Fact]
    public void 内容id里带逗号就整项不写()
    {
        // 分隔符是逗号；真带了逗号宁可丢顺序，也不能写一份读回来是错的
        var layout = new DesktopLayout([new WindowInfo { Id = "X", ContentOrder = ["a,b", "c"] }]);

        var element = ToolWindowLayoutSerializer.Write(layout).Elements("window_info").Single();

        Assert.Null(element.Attribute("content_order"));
    }
}
