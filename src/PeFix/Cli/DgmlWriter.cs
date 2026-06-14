using System.Text;
using PeFix.Meta;

namespace PeFix.Cli;

// Serializes the closure tree to DGML, the dependency-graph format Visual Studio renders.
internal static class DgmlWriter
{
    private const string Ns = "http://schemas.microsoft.com/vs/2009/dgml";

    public static string Render(ClosureReport report)
    {
        Dictionary<string, ClosureNode> nodes = new(StringComparer.Ordinal);
        SortedSet<string> links = new(StringComparer.Ordinal);

        foreach (ClosureTree root in report.Tree ?? [])
            Walk(root, parent: null, nodes, links);

        StringBuilder builder = new();
        builder.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
        builder.Append($"<DirectedGraph xmlns=\"{Ns}\">\n");

        builder.Append("  <Nodes>\n");
        foreach (KeyValuePair<string, ClosureNode> entry in nodes.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            ClosureNode node = entry.Value;
            builder.Append(
                $"    <Node Id=\"{Escape(entry.Key)}\" Label=\"{Escape(node.AssemblyName + " " + node.Version)}\" Category=\"{node.Kind}\" />\n");
        }

        builder.Append("  </Nodes>\n");
        builder.Append("  <Links>\n");
        foreach (string link in links)
            builder.Append(link);

        builder.Append("  </Links>\n");
        builder.Append("  <Categories>\n");
        builder.Append(Category(nameof(ChainKind.Entry), "#FF1B6EC2"));
        builder.Append(Category(nameof(ChainKind.Resolved), "#FF3C9A40"));
        builder.Append(Category(nameof(ChainKind.Provided), "#FF808080"));
        builder.Append(Category(nameof(ChainKind.Unresolved), "#FFC0392B"));
        builder.Append(Category(nameof(ChainKind.Cycle), "#FFD68910"));
        builder.Append("  </Categories>\n");
        builder.Append("</DirectedGraph>\n");
        return builder.ToString();
    }

    private static void Walk(
        ClosureTree node,
        string? parent,
        Dictionary<string, ClosureNode> nodes,
        SortedSet<string> links)
    {
        string id = node.Node.AssemblyName;
        nodes.TryAdd(id, node.Node);
        if (parent is not null)
            links.Add($"    <Link Source=\"{Escape(parent)}\" Target=\"{Escape(id)}\" />\n");

        foreach (ClosureTree child in node.Children)
            Walk(child, id, nodes, links);
    }

    private static string Category(string id, string background) =>
        $"    <Category Id=\"{id}\" Background=\"{background}\" />\n";

    private static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);
}
