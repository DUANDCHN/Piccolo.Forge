using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Piccolo.Forge.Avalonia;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 拖拽期间条带组的排布：被拖的那个拿掉、落点处留一个等大的空槽。
/// </summary>
/// <remarks>
/// 对应 <c>AbstractDroppableStripe.kt</c>：<c>getButtonsToLayOut</c> 里
/// <c>tools.remove(dragButton)</c> 把被拖项摘掉，<c>layoutDragButton</c> 在落点处
/// 把 <c>eachY</c> 推进一个按钮高——后面的按钮因此让位。
/// <para>
/// 这两件在上游是**同一个机制**：蓝色落点框画的就是那个被推出来的空槽。
/// 本仓原来两件都没做（框是浮层另算的），于是既没有让位、位置也和真正的插入点对不上。
/// </para>
/// </remarks>
public class StripeDragLayoutTests
{
    private const double ItemHeight = 40;

    [AvaloniaFact]
    public void The_dragged_item_leaves_the_layout_and_the_rest_move_up()
    {
        var panel = NewPanel(out var items);

        Layout(panel);
        Assert.Equal(ItemHeight, items[1].Bounds.Y);
        Assert.Equal(ItemHeight * 2, items[2].Bounds.Y);

        // 拖走第一个：后两个各往上挪一格
        panel.DraggedChild = items[0];
        Layout(panel);

        Assert.Equal(0, items[1].Bounds.Y);
        Assert.Equal(ItemHeight, items[2].Bounds.Y);
    }

    [AvaloniaFact]
    public void The_reserved_slot_pushes_the_following_items_down()
    {
        var panel = NewPanel(out var items);
        panel.DraggedChild = items[0];
        panel.DropSlotHeight = ItemHeight;

        // 空槽插在剩下两个的中间
        panel.DropIndex = 1;
        Layout(panel);

        Assert.Equal(0, items[1].Bounds.Y);          // 空槽之前，不动
        Assert.NotNull(panel.DropSlotRect);
        Assert.Equal(ItemHeight, panel.DropSlotRect!.Value.Y);
        Assert.Equal(ItemHeight * 2, items[2].Bounds.Y);   // 被空槽顶下去
    }

    [AvaloniaFact]
    public void The_slot_can_sit_at_the_end()
    {
        var panel = NewPanel(out var items);
        panel.DraggedChild = items[0];
        panel.DropSlotHeight = ItemHeight;
        panel.DropIndex = 2;   // 剩两个，插在末尾
        Layout(panel);

        Assert.NotNull(panel.DropSlotRect);
        Assert.Equal(ItemHeight * 2, panel.DropSlotRect!.Value.Y);
    }

    /// <summary>
    /// 没在拖的时候不留槽——<see cref="StripeItemsPanel.DropIndex"/> 的默认值是 -1。
    /// </summary>
    [AvaloniaFact]
    public void No_slot_when_nothing_is_being_dragged()
    {
        var panel = NewPanel(out _);
        Layout(panel);

        Assert.Null(panel.DropSlotRect);
    }

    private static StripeItemsPanel NewPanel(out List<Control> items)
    {
        var panel = new StripeItemsPanel();
        items = [];

        for (var i = 0; i < 3; i++)
        {
            var item = new Border { Width = 37, Height = ItemHeight };
            items.Add(item);
            panel.Children.Add(item);
        }

        return panel;
    }

    private static void Layout(StripeItemsPanel panel)
    {
        panel.InvalidateMeasure();
        panel.Measure(new Size(100, 1000));
        panel.Arrange(new Rect(panel.DesiredSize));
    }
}
