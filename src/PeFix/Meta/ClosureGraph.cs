namespace PeFix.Meta;

public static class ClosureGraph
{
    private const int DepthCap = 64;

    public static ClosureReport Build(IReadOnlyList<Inspection> inspections, string directory)
    {
        ArgumentNullException.ThrowIfNull(inspections);

        WalkCtx ctx = new(DepIndex.Build(inspections));
        List<string> entries = [];

        foreach (Inspection entry in inspections)
        {
            if (!entry.AssemblyDef.HasValue)
                continue;

            AsmRef def = entry.AssemblyDef.Value;
            entries.Add(def.Name);
            ClosureNode entryNode = new(def.Name, def.Version, ChainKind.Entry);

            AsmRef[] refs = entry.AssemblyRefs ?? [];
            if (refs.Length == 0)
                continue;

            foreach (AsmRef directRef in refs)
            {
                WalkRef(entryNode, directRef, ctx);
            }
        }

        return new ClosureReport(
            directory,
            entries.ToArray(),
            ctx.Unresolved(),
            ctx.Cycles(),
            ctx.RefsWalked,
            ctx.FrameworkLeaves);
    }

    private static void WalkRef(ClosureNode entryNode, AsmRef directRef, WalkCtx ctx)
    {
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase) { entryNode.AssemblyName };
        Stack<WalkFrm> stack = new();
        stack.Push(new WalkFrm(directRef, new WalkTrail([], 0, visited)));

        while (stack.Count > 0)
        {
            WalkFrm frm = stack.Pop();
            ctx.CountRef();

            ProvidedKind provided = DepIndex.ClassifyProvided(frm.Ref.Name);
            if (provided != ProvidedKind.None)
            {
                if (provided is ProvidedKind.Framework)
                    ctx.CountFramework();
                continue;
            }

            if (frm.Depth >= DepthCap)
            {
                ctx.Emit(entryNode, frm.Trail.Path, Leaf(frm.Ref, ChainKind.Cycle));
                continue;
            }

            if (!ctx.Deps.TryGetProvider(frm.Ref.Name, out Inspection nextProv))
            {
                ctx.Emit(entryNode, frm.Trail.Path, Leaf(frm.Ref, ChainKind.Unresolved));
                continue;
            }

            if (frm.Trail.Visiting.Contains(frm.Ref.Name))
            {
                ctx.Emit(entryNode, frm.Trail.Path, Leaf(frm.Ref, ChainKind.Cycle));
                continue;
            }

            ClosureNode resNode = new(frm.Ref.Name, frm.Ref.Version, ChainKind.Resolved);
            WalkTrail nextTrail = frm.Trail.Next(frm.Ref.Name, resNode);

            AsmRef[] nextRefs = nextProv.AssemblyRefs ?? [];
            for (int i = nextRefs.Length - 1; i >= 0; i--)
            {
                stack.Push(new WalkFrm(nextRefs[i], nextTrail));
            }
        }
    }

    private static ClosureNode Leaf(AsmRef asmRef, ChainKind kind)
    {
        return new ClosureNode(asmRef.Name, asmRef.Version, kind);
    }

    private sealed class WalkFrm
    {
        public AsmRef Ref { get; }
        public WalkTrail Trail { get; }
        public int Depth => Trail.Depth;

        public WalkFrm(AsmRef asmRef, WalkTrail trail)
        {
            Ref = asmRef;
            Trail = trail;
        }
    }

    private sealed class WalkTrail
    {
        public List<ClosureNode> Path { get; }
        public int Depth { get; }
        public HashSet<string> Visiting { get; }

        public WalkTrail(List<ClosureNode> path, int depth, HashSet<string> visiting)
        {
            Path = path;
            Depth = depth;
            Visiting = visiting;
        }

        public WalkTrail Next(string name, ClosureNode node)
        {
            HashSet<string> newVis = new(Visiting, StringComparer.OrdinalIgnoreCase)
                { name };
            List<ClosureNode> newPath = new(Path) { node };
            return new WalkTrail(newPath, Depth + 1, newVis);
        }
    }

    private sealed class WalkCtx
    {
        private readonly List<ClosureChain> _chains = [];
        private readonly HashSet<string> _emitted = new(StringComparer.OrdinalIgnoreCase);

        public WalkCtx(DepIndex deps)
        {
            Deps = deps;
        }

        public DepIndex Deps { get; }
        public int RefsWalked { get; private set; }
        public int FrameworkLeaves { get; private set; }

        public void CountRef() => RefsWalked++;

        public void CountFramework() => FrameworkLeaves++;

        public ClosureChain[] Unresolved()
        {
            return _chains
                .Where(c => c.Segments.Length > 0 && c.Segments[^1].Kind == ChainKind.Unresolved)
                .ToArray();
        }

        public ClosureChain[] Cycles()
        {
            return _chains
                .Where(c => c.Segments.Length > 0 && c.Segments[^1].Kind == ChainKind.Cycle)
                .ToArray();
        }

        public void Emit(ClosureNode entryNode, List<ClosureNode> path, ClosureNode leaf)
        {
            List<ClosureNode> fullPath = new(path) { leaf };
            string key = $"{entryNode.AssemblyName}\0{leaf.AssemblyName}".ToLowerInvariant();
            if (_emitted.Add(key))
                _chains.Add(new ClosureChain(entryNode, fullPath.ToArray()));
        }
    }
}
