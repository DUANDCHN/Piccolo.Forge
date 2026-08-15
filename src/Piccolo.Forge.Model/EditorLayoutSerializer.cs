using System.Globalization;
using System.Xml.Linq;

namespace Piccolo.Forge.Model;

/// <summary>
/// 编辑区的读写，XML 形状与属性名照抄上游。
/// </summary>
/// <remarks>
/// 出处见 <c>docs/upstream/编辑区.md</c> §4
/// （<c>EditorsSplitters.kt:1267-1294 · writePanel</c>、<c>FileEntry.kt:32-33</c>）：
/// <code>
/// &lt;splitter split-orientation="vertical" split-proportion="0.5"&gt;
///   &lt;split-first&gt;  …&lt;/split-first&gt;
///   &lt;split-second&gt; …&lt;/split-second&gt;
/// &lt;/splitter&gt;
/// </code>
/// 叶子是 <c>&lt;leaf&gt;</c>，里面每页一个 <c>&lt;file&gt;</c>。
/// </remarks>
public static class EditorLayoutSerializer
{
    /// <summary>分屏节点的标签（<c>EditorsSplitters.kt:1270</c>）。</summary>
    public const string SplitterTag = "splitter";

    /// <summary>第一块（<c>:1273</c>）。</summary>
    public const string FirstTag = "split-first";

    /// <summary>第二块（<c>:1275</c>）。</summary>
    public const string SecondTag = "split-second";

    /// <summary>叶子（<c>:1283</c>）。</summary>
    public const string LeafTag = "leaf";

    /// <summary>单个标签页（<c>FileEntry.kt:102</c>）。</summary>
    public const string FileTag = "file";

    /// <summary>写成 XML。</summary>
    /// <param name="area">编辑区。</param>
    /// <returns>根元素。</returns>
    public static XElement Write(EditorArea area)
    {
        ArgumentNullException.ThrowIfNull(area);
        return WriteNode(area.Root, area.CurrentGroup);
    }

    /// <summary>
    /// 从 XML 读回来。
    /// </summary>
    /// <remarks>
    /// 比例读不到时**兜底 0.5**（<c>EditorsSplitters.kt:1329</c>）；
    /// <c>split-orientation</c> 只有等于 <c>"vertical"</c> 才算竖切（<c>:1326</c>）。
    /// </remarks>
    /// <param name="root">根元素。</param>
    /// <returns>读出来的编辑区。</returns>
    public static EditorArea Read(XElement root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var area = new EditorArea();
        Read(root, area);
        return area;
    }

    /// <summary>
    /// 读回**已有的**编辑区里 —— 宿主的编辑区通常在装配期就建好了，
    /// 存档是之后才读到的，这时候不能换对象，只能换里面那棵树。
    /// </summary>
    /// <param name="root">根元素。</param>
    /// <param name="area">要覆盖的编辑区。</param>
    public static void Read(XElement root, EditorArea area)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(area);

        EditorGroup? current = null;
        var node = ReadNode(root, ref current);

        area.ReplaceRoot(node, current ?? node.Groups.First());
    }

    private static XElement WriteNode(EditorNode node, EditorGroup currentGroup)
    {
        if (node is EditorSplit split)
        {
            var element = new XElement(SplitterTag);
            element.SetAttributeValue("split-orientation", split.IsVertical ? "vertical" : "horizontal");
            element.SetAttributeValue(
                "split-proportion", split.Proportion.ToString("R", CultureInfo.InvariantCulture));
            element.Add(new XElement(FirstTag, WriteNode(split.First, currentGroup)));
            element.Add(new XElement(SecondTag, WriteNode(split.Second, currentGroup)));
            return element;
        }

        var group = (EditorGroup)node;
        var leaf = new XElement(LeafTag);

        // 「当前是哪一组」也要存，否则读回来焦点落在别处
        if (ReferenceEquals(group, currentGroup))
        {
            leaf.SetAttributeValue("current", "true");
        }

        foreach (var tab in group.Tabs)
        {
            var file = new XElement(FileTag);
            file.SetAttributeValue("id", tab.Id);
            file.SetAttributeValue("title", tab.Title);

            // 只写与默认不同的（FileEntry.kt:115-122）
            if (tab.IsPinned)
            {
                file.SetAttributeValue("pinned", "true");
            }

            if (tab.Id == group.SelectedId)
            {
                file.SetAttributeValue("current-in-tab", "true");
            }

            if (!tab.IsCloseable)
            {
                file.SetAttributeValue("excluded-from-tab-limit", "true");
            }

            leaf.Add(file);
        }

        return leaf;
    }

    private static EditorNode ReadNode(XElement element, ref EditorGroup? current)
    {
        if (element.Name == SplitterTag)
        {
            var first = element.Element(FirstTag)?.Elements().FirstOrDefault();
            var second = element.Element(SecondTag)?.Elements().FirstOrDefault();

            if (first is null || second is null)
            {
                return new EditorGroup();
            }

            var isVertical = string.Equals(
                (string?)element.Attribute("split-orientation"), "vertical", StringComparison.Ordinal);

            var proportion = float.TryParse(
                (string?)element.Attribute("split-proportion"), CultureInfo.InvariantCulture, out var value)
                ? value
                : EditorSplit.DefaultProportion;

            return new EditorSplit(ReadNode(first, ref current), ReadNode(second, ref current), isVertical, proportion);
        }

        var group = new EditorGroup();
        string? selected = null;

        foreach (var file in element.Elements(FileTag))
        {
            var id = (string?)file.Attribute("id");
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            group.Add(
                new EditorTab(
                    id,
                    (string?)file.Attribute("title") ?? id,
                    IsPinned: Bool(file, "pinned"),
                    IsCloseable: !Bool(file, "excluded-from-tab-limit")),
                select: false);

            if (Bool(file, "current-in-tab"))
            {
                selected = id;
            }
        }

        if (selected is not null)
        {
            group.Select(selected);
        }

        if (Bool(element, "current"))
        {
            current = group;
        }

        return group;
    }

    private static bool Bool(XElement element, string name)
        => bool.TryParse((string?)element.Attribute(name), out var value) && value;
}
