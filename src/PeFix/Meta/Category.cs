namespace PeFix.Meta;

public enum Category
{
    Portability,    // was ManagedPePortability
    RefAssembly,    // was ReferenceAssemblyMisuse
    NativeBinary,   // was NonRewritableBinary (pure native, no CLI header)
    MixedMode       // new: C++/CLI mixed-mode assemblies
}
