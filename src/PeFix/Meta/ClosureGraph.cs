namespace PeFix.Meta;

public static class ClosureGraph
{
    private const int DepthCap = 64;

    public static ClosureReport Build(
        IReadOnlyList<Inspection> inspections,
        string directory,
        HostProfile? hostProfile = null)
    {
        ArgumentNullException.ThrowIfNull(inspections);

        WalkCtx ctx = new(DependencyIndex.Build(inspections, hostProfile));
        List<string> entries = [];

        foreach (Inspection entry in inspections)
        {
            if (!entry.AssemblyDefinition.HasValue)
                continue;

            AssemblyIdentity def = entry.AssemblyDefinition.Value;
            entries.Add(def.Name);
            ClosureNode entryNode = new(def.Name, def.Version, ChainKind.Entry);

            AssemblyIdentity[] references = entry.AssemblyReferences ?? [];
            if (references.Length == 0)
                continue;

            foreach (AssemblyIdentity directRef in references)
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
            ctx.ProvidedLeaves());
    }

    private static void WalkRef(ClosureNode entryNode, AssemblyIdentity directRef, WalkCtx ctx)
    {
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase) { entryNode.AssemblyName };
        Stack<WalkFrm> stack = new();
        stack.Push(new WalkFrm(directRef, new WalkTrail([], 0, visited)));

        while (stack.Count > 0)
        {
            WalkFrm frm = stack.Pop();
            ctx.CountRef();

            ProvidedKind provided = ctx.Deps.ClassifyProvided(frm.Ref.Name);
            if (provided != ProvidedKind.None)
            {
                ctx.CountProvidedLeaf(provided);
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

            AssemblyIdentity[] nextRefs = nextProv.AssemblyReferences ?? [];
            for (int i = nextRefs.Length - 1; i >= 0; i--)
            {
                stack.Push(new WalkFrm(nextRefs[i], nextTrail));
            }
        }
    }

    private static ClosureNode Leaf(AssemblyIdentity assemblyReference, ChainKind kind)
    {
        return new ClosureNode(assemblyReference.Name, assemblyReference.Version, kind);
    }

    private sealed class WalkFrm
    {
        public AssemblyIdentity Ref { get; }
        public WalkTrail Trail { get; }
        public int Depth => Trail.Depth;

        public WalkFrm(AssemblyIdentity assemblyReference, WalkTrail trail)
        {
            Ref = assemblyReference;
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

        public WalkCtx(DependencyIndex dependencies)
        {
            Deps = dependencies;
        }

        public DependencyIndex Deps { get; }
        public int RefsWalked { get; private set; }
        private int TotalProvidedLeaves { get; set; }
        private int FrameworkProvidedLeaves { get; set; }

        public void CountRef() => RefsWalked++;

        public void CountProvidedLeaf(ProvidedKind kind)
        {
            TotalProvidedLeaves++;
            if (kind is ProvidedKind.Framework)
                FrameworkProvidedLeaves++;
        }

        public ProvidedLeafCounts ProvidedLeaves()
        {
            return new ProvidedLeafCounts(TotalProvidedLeaves, FrameworkProvidedLeaves);
        }

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
