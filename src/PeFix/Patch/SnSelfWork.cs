using PeFix.Plan;

namespace PeFix.Patch;

internal sealed record SnSelfWork
{
    public required byte[] Patched { get; init; }
    public required bool HadIvt { get; init; }
    public required string AssemblyName { get; init; }
    public required bool WasSigned { get; init; }
    public required IReadOnlyList<MutationOp> Ops { get; init; }
}
