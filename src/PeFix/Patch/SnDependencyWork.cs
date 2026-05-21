using PeFix.Plan;

namespace PeFix.Patch;

internal readonly record struct SnDependencyWork(byte[] Patched, MutationOp[] Ops);
