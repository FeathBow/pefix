namespace PeFix.Meta;

public static class ClosureGraph
{
    private const int DepthCap = 64;

    public static ClosureReport Build(
        IReadOnlyList<Inspection> inspections,
        string directory,
        HostProfile? hostProfile = null,
        IReadOnlySet<string>? declaredAssets = null)
    {
        return BuildCore(inspections, directory, new GraphOpts(hostProfile, false), declaredAssets);
    }

    public static ClosureReport BuildTree(
        IReadOnlyList<Inspection> inspections,
        string directory,
        HostProfile? hostProfile = null,
        IReadOnlySet<string>? declaredAssets = null)
    {
        return BuildCore(inspections, directory, new GraphOpts(hostProfile, true), declaredAssets);
    }

    private static ClosureReport BuildCore(
        IReadOnlyList<Inspection> inspections,
        string directory,
        GraphOpts opts,
        IReadOnlySet<string>? declaredAssets)
    {
        ArgumentNullException.ThrowIfNull(inspections);

        WalkCtx ctx = new(DependencyIndex.Build(inspections, opts.HostProfile, declaredAssets));
        List<string> entries = [];
        List<ClosureTree> tree = [];

        foreach (Inspection entry in inspections)
        {
            if (!entry.AssemblyDefinition.HasValue)
                continue;

            AssemblyIdentity def = entry.AssemblyDefinition.Value;
            entries.Add(def.Name);
            ClosureNode entryNode = new(def.Name, def.Version, ChainKind.Entry);
            TreeBld? root = opts.Tree ? new TreeBld(entryNode) : null;

            AssemblyIdentity[] references = entry.AssemblyReferences ?? [];
            foreach (AssemblyIdentity directRef in references)
            {
                WalkRef(new WalkReq(entryNode, directRef, root), ctx);
            }

            if (root is not null)
                tree.Add(root.ToTree());
        }

        return new ClosureReport(
            directory,
            entries.ToArray(),
            ctx.Unresolved(),
            ctx.Cycles(),
            ctx.RefsWalked,
            ctx.ProvidedLeaves(),
            opts.Tree ? tree.ToArray() : null);
    }

    private static void WalkRef(WalkReq req, WalkCtx ctx)
    {
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase) { req.Entry.AssemblyName };
        Stack<WalkFrm> stack = new();
        stack.Push(new WalkFrm(req.Ref, new WalkTrail([], 0, visited), req.Root));

        while (stack.Count > 0)
        {
            WalkFrm frm = stack.Pop();
            ctx.CountRef();

            ProvidedKind provided = ctx.Deps.ClassifyProvided(frm.Ref.Name);
            if (provided != ProvidedKind.None)
            {
                ctx.CountProvidedLeaf(provided);
                AddLeaf(frm, ChainKind.Provided);
                continue;
            }

            if (frm.Depth >= DepthCap)
            {
                EmitLeaf(new LeafHit(req, frm, ChainKind.Cycle), ctx);
                continue;
            }

            if (!ctx.Deps.TryGetProvider(frm.Ref.Name, out Inspection nextProv))
            {
                EmitLeaf(new LeafHit(req, frm, ChainKind.Unresolved), ctx);
                continue;
            }

            if (frm.Trail.Visiting.Contains(frm.Ref.Name))
            {
                EmitLeaf(new LeafHit(req, frm, ChainKind.Cycle), ctx);
                continue;
            }

            ClosureNode resNode = new(frm.Ref.Name, frm.Ref.Version, ChainKind.Resolved);
            TreeBld? resTree = frm.Parent?.Add(resNode);
            WalkTrail nextTrail = frm.Trail.Next(frm.Ref.Name, resNode);

            AssemblyIdentity[] nextRefs = nextProv.AssemblyReferences ?? [];
            for (int i = nextRefs.Length - 1; i >= 0; i--)
            {
                stack.Push(new WalkFrm(nextRefs[i], nextTrail, resTree));
            }
        }
    }

    private static void EmitLeaf(LeafHit hit, WalkCtx ctx)
    {
        ClosureNode leaf = AddLeaf(hit.Frame, hit.Kind);
        ctx.Emit(hit.Req.Entry, hit.Frame.Trail.Path, leaf);
    }

    private static ClosureNode AddLeaf(WalkFrm frame, ChainKind kind)
    {
        ClosureNode leaf = Leaf(frame.Ref, kind);
        frame.Parent?.Add(leaf);
        return leaf;
    }

    private static ClosureNode Leaf(AssemblyIdentity assemblyReference, ChainKind kind)
    {
        return new ClosureNode(assemblyReference.Name, assemblyReference.Version, kind);
    }

    private sealed class WalkFrm
    {
        public AssemblyIdentity Ref { get; }
        public WalkTrail Trail { get; }
        public TreeBld? Parent { get; }
        public int Depth => Trail.Depth;

        public WalkFrm(AssemblyIdentity assemblyReference, WalkTrail trail, TreeBld? parent)
        {
            Ref = assemblyReference;
            Trail = trail;
            Parent = parent;
        }
    }

    private readonly record struct GraphOpts(HostProfile? HostProfile, bool Tree);

    private readonly record struct WalkReq(
        ClosureNode Entry,
        AssemblyIdentity Ref,
        TreeBld? Root);

    private readonly record struct LeafHit(
        WalkReq Req,
        WalkFrm Frame,
        ChainKind Kind);

    private sealed class TreeBld
    {
        private readonly List<TreeBld> _children = [];

        public TreeBld(ClosureNode node)
        {
            Node = node;
        }

        public ClosureNode Node { get; }

        public TreeBld Add(ClosureNode node)
        {
            var child = new TreeBld(node);
            _children.Add(child);
            return child;
        }

        public ClosureTree ToTree()
        {
            return new ClosureTree(Node, [.. _children.Select(child => child.ToTree())]);
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
