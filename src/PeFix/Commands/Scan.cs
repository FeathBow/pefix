using PeFix.Cli;
using PeFix.Meta;

namespace PeFix.Commands;

internal static class Scan
{
    internal static int Run(string path, bool json, string? failOn, bool onConflict)
    {
        Status? threshold = null;
        if (failOn is not null)
        {
            if (!SevArg.TryParse(failOn, out Status value))
                return SevArg.WriteBad(failOn);

            threshold = value;
        }

        ScanReport report = Scanner.Scan(path);
        if (json)
        {
            JsonOut.Write(JsonWriter.Render(report));
        }
        else
        {
            Console.WriteLine(ScanWriter.Render(report));
        }

        if (onConflict && report.Conflicts.Length > 0)
            return 1;

        return threshold is { } t && report.Results.Any(r => r.Status >= t) ? 1 : 0;
    }
}
