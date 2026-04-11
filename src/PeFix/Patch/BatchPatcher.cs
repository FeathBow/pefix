using PeFix.Meta;

namespace PeFix.Patch;

public static class BatchPatcher
{
    public static BatchResult Fix(string path, PatchOptions options)
    {
        ScanReport report = Scanner.Scan(path);
        var results = new List<PatchResult>(report.Results.Length);
        var refusals = new List<Refusal>();

        foreach (Inspection inspection in report.Results)
        {
            try
            {
                results.Add(Patcher.Fix(inspection.Path, options));
            }
            catch (UnsafeException ex)
            {
                refusals.Add(new Refusal(inspection.Path, ex.Message, inspection));
            }
        }

        return new BatchResult(report.Directory, results.ToArray(), refusals.ToArray());
    }
}
