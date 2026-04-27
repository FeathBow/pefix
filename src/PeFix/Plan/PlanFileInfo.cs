using System.Security.Cryptography;

namespace PeFix.Plan;

public static class PlanFileInfo
{
    public static PlanFile Describe(string path, byte[] bytes, string mvid)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return new PlanFile(
            path,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            bytes.Length,
            mvid);
    }
}
