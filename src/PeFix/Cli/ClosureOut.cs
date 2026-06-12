using System.Globalization;
using PeFix.Meta;

namespace PeFix.Cli;

internal static class ClosureOut
{
    private const int ChainBaseIndent = 4;
    private const int ChainIndentStep = 2;
    private const int TagGap = 2;

    public static string Render(ClosureReport report)
    {
        using var writer = new StringWriter();
        WriteBlock(writer, report);
        WriteChains(writer, report);
        WriteTree(writer, report);
        WriteOrphans(writer, report);
        return writer.ToString().TrimEnd();
    }

    private static void WriteBlock(StringWriter writer, ClosureReport report)
    {
        bool hasMissing = report.Unresolved.Length > 0;
        string dirName = Path.GetFileName(report.Directory);
        string status = hasMissing ? "UNRESOLVED" : "RESOLVED";
        string summary = string.Format(
            CultureInfo.InvariantCulture,
            "{0} entry assemblies, {1} transitive references, {2} unresolved leaves, {3} cycles.",
            report.Entries.Length,
            report.RefsWalked,
            report.Unresolved.Length,
            report.CycleChains.Length);
        string action = hasMissing
            ? "Add the missing dependencies to the scanned directory or restore their packages."
            : "All transitive references are accounted for; no missing dependencies detected.";

        var details = new (string, string)[]
        {
            ("Entry Assemblies:", report.Entries.Length.ToString(CultureInfo.InvariantCulture)),
            ("Transitive Refs:", report.RefsWalked.ToString(CultureInfo.InvariantCulture)),
            ("Unresolved:", report.Unresolved.Length.ToString(CultureInfo.InvariantCulture)),
            ("Cycles:", report.CycleChains.Length.ToString(CultureInfo.InvariantCulture)),
            ("Provided Leaves:", report.ProvidedLeaves.Total.ToString(CultureInfo.InvariantCulture)),
        };

        var block = new MutBlock(dirName, "closure", status, summary, action, details);
        writer.WriteLine(block.Render());
    }

    private static void WriteChains(StringWriter writer, ClosureReport report)
    {
        if (report.Unresolved.Length == 0 && report.CycleChains.Length == 0)
            return;

        if (report.Unresolved.Length > 0)
        {
            writer.WriteLine();
            writer.WriteLine("  Unresolved chains:");

            foreach (ClosureChain chain in report.Unresolved)
            {
                WriteChain(writer, chain, "MISSING");
            }
        }

        if (report.CycleChains.Length > 0)
        {
            writer.WriteLine();
            writer.WriteLine("  Cycle chains:");

            foreach (ClosureChain chain in report.CycleChains)
            {
                WriteChain(writer, chain, "CYCLE");
            }
        }
    }

    private static void WriteChain(StringWriter writer, ClosureChain chain, string leafLabel)
    {
        writer.WriteLine($"    {chain.Entry.AssemblyName}.dll");
        string[] lefts = ChainLefts(chain);
        int tagCol = lefts.Max(left => left.Length) + TagGap;

        for (int i = 0; i < lefts.Length; i++)
        {
            bool isLast = i == chain.Segments.Length - 1;
            string tag = isLast ? leafLabel : "resolved";
            writer.WriteLine($"{lefts[i]}{TagPad(lefts[i], tagCol)}[{tag}]");
        }

        writer.WriteLine();
    }

    private static void WriteTree(StringWriter writer, ClosureReport report)
    {
        if (report.Tree is not { } tree)
            return;

        if (!HasChains(report))
            writer.WriteLine();

        writer.WriteLine("  Dependency tree:");
        foreach (ClosureTree root in tree)
            WriteTreeRoot(writer, root);
    }

    private static void WriteTreeRoot(StringWriter writer, ClosureTree root)
    {
        List<TreeLine> lines = [];
        AddTreeLines(root, 0, lines);
        int tagCol = lines.Max(line => line.Left.Length) + TagGap;

        foreach (TreeLine line in lines)
            writer.WriteLine($"{line.Left}{TagPad(line.Left, tagCol)}[{line.Tag}]");

        writer.WriteLine();
    }

    private static void AddTreeLines(ClosureTree tree, int depth, List<TreeLine> lines)
    {
        lines.Add(new TreeLine(TreeLeft(tree.Node, depth), TreeTag(tree.Node.Kind)));
        foreach (ClosureTree child in tree.Children)
            AddTreeLines(child, depth + 1, lines);
    }

    private static void WriteOrphans(StringWriter writer, ClosureReport report)
    {
        if (report.Orphans is not { } orphans)
            return;

        writer.WriteLine();
        if (orphans.Length == 0)
        {
            writer.WriteLine("  Unreferenced: none found; every managed assembly is referenced, an entry point, a plugin, or a satellite.");
            return;
        }

        writer.WriteLine($"  Unreferenced ({orphans.Length}):");
        PathRelativizer rel = new(report.Directory);
        foreach (string orphan in orphans)
            writer.WriteLine($"    - {rel.RelativePath(orphan)}");

        writer.WriteLine("  Note: unreferenced is advisory; host configuration or dynamic loading outside literal reflection is not observed.");
    }

    private static string[] ChainLefts(ClosureChain chain)
    {
        string[] lefts = new string[chain.Segments.Length];
        for (int i = 0; i < chain.Segments.Length; i++)
        {
            ClosureNode seg = chain.Segments[i];
            string indent = new(' ', ChainBaseIndent + (i + 1) * ChainIndentStep);
            lefts[i] = $"{indent}\u2192 {seg.AssemblyName}.dll v{seg.Version}";
        }

        return lefts;
    }

    private static string TagPad(string left, int tagCol)
    {
        if (left.Length >= tagCol)
            return " ";

        return new string(' ', tagCol - left.Length);
    }

    private static bool HasChains(ClosureReport report)
    {
        return report.Unresolved.Length > 0 || report.CycleChains.Length > 0;
    }

    private static string TreeLeft(ClosureNode node, int depth)
    {
        string indent = new(' ', ChainBaseIndent + depth * ChainIndentStep);
        string text = $"{node.AssemblyName}.dll v{node.Version}";
        return depth == 0 ? $"{indent}{text}" : $"{indent}\u2192 {text}";
    }

    private static string TreeTag(ChainKind kind) => kind switch
    {
        ChainKind.Entry or ChainKind.Resolved => "resolved",
        ChainKind.Unresolved => "MISSING",
        ChainKind.Cycle => "cycle",
        ChainKind.Provided => "provided",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private readonly record struct TreeLine(string Left, string Tag);
}
