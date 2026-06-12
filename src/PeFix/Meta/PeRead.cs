using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace PeFix.Meta;

internal readonly record struct PeRead(
    string Path,
    PEReader Reader,
    MetadataReader Metadata);
