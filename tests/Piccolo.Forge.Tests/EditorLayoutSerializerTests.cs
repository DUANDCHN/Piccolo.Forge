using System.Xml.Linq;
using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 编辑区存档的形状。
public class EditorLayoutSerializerTests
{
    [Fact]
    public void Tags_match_upstream()
    {
        // EditorsSplitters.kt:1270-1283、FileEntry.kt:102
        Assert.Equal("splitter", EditorLayoutSerializer.SplitterTag);
        Assert.Equal("split-first", EditorLayoutSerializer.FirstTag);
        Assert.Equal("split-second", EditorLayoutSerializer.SecondTag);
        Assert.Equal("leaf", EditorLayoutSerializer.LeafTag);
        Assert.Equal("file", EditorLayoutSerializer.FileTag);
    }

    [Fact]
    public void A_single_group_writes_a_bare_leaf()
    {
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));

        var xml = EditorLayoutSerializer.Write(area);

        Assert.Equal("leaf", xml.Name);
        Assert.Single(xml.Elements("file"));
    }

    [Fact]
    public void Splits_nest_as_first_and_second()
    {
        // EditorsSplitters.kt:1270-1279
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));
        area.Split(area.CurrentGroup, isVertical: true);

        var xml = EditorLayoutSerializer.Write(area);

        Assert.Equal("splitter", xml.Name);
        Assert.Equal("vertical", (string?)xml.Attribute("split-orientation"));
        Assert.NotNull(xml.Element("split-first")!.Element("leaf"));
        Assert.NotNull(xml.Element("split-second")!.Element("leaf"));
    }

    [Fact]
    public void Only_non_default_flags_are_written()
    {
        // FileEntry.kt:115-122
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));

        var file = EditorLayoutSerializer.Write(area).Elements("file").Single();

        Assert.Null(file.Attribute("pinned"));
        Assert.Equal("true", (string?)file.Attribute("current-in-tab"));   // 它是选中的
    }

    [Fact]
    public void Pinned_survives_a_round_trip()
    {
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));
        area.Open(new EditorTab("b", "B"));
        area.SetPinned("a", pinned: true);

        var reloaded = EditorLayoutSerializer.Read(EditorLayoutSerializer.Write(area));

        var group = reloaded.Groups.Single();
        Assert.True(group.Tabs.Single(x => x.Id == "a").IsPinned);
        Assert.Equal("b", group.SelectedId);
    }

    [Fact]
    public void The_split_tree_survives_a_round_trip()
    {
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));
        area.Open(new EditorTab("b", "B"));
        var right = area.Split(area.CurrentGroup, isVertical: false)!;
        right.Add(new EditorTab("c", "C"));

        var reloaded = EditorLayoutSerializer.Read(EditorLayoutSerializer.Write(area));

        Assert.Equal(2, reloaded.Groups.Count);
        var split = Assert.IsType<EditorSplit>(reloaded.Root);
        Assert.False(split.IsVertical);
        Assert.Equal(["a", "b"], reloaded.Groups[0].Tabs.Select(x => x.Id));
        Assert.Equal(["b", "c"], reloaded.Groups[1].Tabs.Select(x => x.Id));
    }

    [Fact]
    public void The_current_group_survives_a_round_trip()
    {
        var area = new EditorArea();
        area.Open(new EditorTab("a", "A"));
        var created = area.Split(area.CurrentGroup, isVertical: true)!;
        Assert.Same(created, area.CurrentGroup);

        var reloaded = EditorLayoutSerializer.Read(EditorLayoutSerializer.Write(area));

        Assert.Same(reloaded.Groups[1], reloaded.CurrentGroup);
    }

    [Fact]
    public void A_missing_proportion_falls_back_to_half()
    {
        // EditorsSplitters.kt:1329
        var xml = XElement.Parse(
            """
            <splitter split-orientation="horizontal">
              <split-first><leaf><file id="a" /></leaf></split-first>
              <split-second><leaf><file id="b" /></leaf></split-second>
            </splitter>
            """);

        var split = Assert.IsType<EditorSplit>(EditorLayoutSerializer.Read(xml).Root);

        Assert.Equal(EditorSplit.DefaultProportion, split.Proportion);
    }

    [Fact]
    public void Files_without_an_id_are_dropped()
    {
        var xml = XElement.Parse("""<leaf><file /><file id="a" /></leaf>""");

        Assert.Single(EditorLayoutSerializer.Read(xml).Groups.Single().Tabs);
    }
}
