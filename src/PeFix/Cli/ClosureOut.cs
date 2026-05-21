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
            ("Framework Leaves:", report.FrameworkLeaves.ToString(CultureInfo.InvariantCulture)),
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
}
