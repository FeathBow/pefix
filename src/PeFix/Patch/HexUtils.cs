namespace PeFix.Patch;

internal static class HexUtils
{
    internal static string Hex(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(bytes).ToLowerInvariant();
}
