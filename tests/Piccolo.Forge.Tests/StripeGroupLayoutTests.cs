using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// AbstractDroppableStripe.getButtonsToLayOut 的定序与分隔符插入。
public class StripeGroupLayoutTests
{
    [Fact]
    public void Separator_goes_in_front_of_the_first_split_button()
    {
        // AbstractDroppableStripe.kt:590-596
        var items = StripeGroupLayout.Build(
            [
                new WindowInfo { Id = "Project", Anchor = ToolWindowAnchor.Left, Order = 0 },
                new WindowInfo { Id = "Commit", Anchor = ToolWindowAnchor.Left, Order = 1 },
                new WindowInfo { Id = "Structure", Anchor = ToolWindowAnchor.Left, Order = 2, IsSplit = true },
            ],
            ToolWindowAnchor.Left,
            useSeparator: true);

        Assert.Equal(["Project", "Commit", null, "Structure"], items.Select(x => x.Window?.Id));
        Assert.True(items[2].IsSeparator);
    }

    [Fact]
    public void No_separator_when_nothing_is_split()
    {
        var items = StripeGroupLayout.Build(
            [
                new WindowInfo { Id = "Project", Order = 0 },
                new WindowInfo { Id = "Commit", Order = 1 },
            ],
            ToolWindowAnchor.Left,
            useSeparator: true);

        Assert.DoesNotContain(items, x => x.IsSeparator);
    }

    [Fact]
    public void No_separator_when_the_whole_group_is_split()
    {
        // 落在索引 0 → 非拖拽时去掉（AbstractDroppableStripe.kt:602-611）
        var items = StripeGroupLayout.Build(
            [
                new WindowInfo { Id = "a", Order = 0, IsSplit = true },
                new WindowInfo { Id = "b", Order = 1, IsSplit = true },
            ],
            ToolWindowAnchor.Left,
            useSeparator: true);

        Assert.DoesNotContain(items, x => x.IsSeparator);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void Bottom_group_never_uses_a_separator_and_is_reversed()
    {
        // BOTTOM 组落在左/右条带的下半组，不是竖条带的 LEFT/RIGHT 那一组
        // （useStripeButtonSeparator 只对 LEFT/RIGHT 为真，:99-105）
        var items = StripeGroupLayout.Build(
            [
                new WindowInfo { Id = "Version Control", Anchor = ToolWindowAnchor.Bottom, Order = 0 },
                new WindowInfo { Id = "Terminal", Anchor = ToolWindowAnchor.Bottom, Order = 1 },
            ],
            ToolWindowAnchor.Bottom,
            useSeparator: false);

        Assert.Equal(["Terminal", "Version Control"], items.Select(x => x.Window!.Id));
    }

    [Fact]
    public void Default_layout_left_group_gets_a_separator_before_Structure()
    {
        // 默认布局里 Structure 是左边唯一的 split（defaultToolWindowlayoutProvider.kt:263-266）
        var layout = DefaultToolWindowLayout.CreateV2();
        var left = layout.GetSortedList().Where(x => x.Anchor == ToolWindowAnchor.Left);

        var items = StripeGroupLayout.Build(left, ToolWindowAnchor.Left, useSeparator: true);

        Assert.Equal(["Project", "Commit", null, "Structure"], items.Select(x => x.Window?.Id));
    }
}
