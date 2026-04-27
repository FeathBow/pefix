using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace PeFix.Tests;

internal static class PeRead
{
    internal static T Pe<T>(string path, Func<PEReader, T> read)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var pe = new PEReader(stream);
        return read(pe);
    }

    internal static T Meta<T>(string path, Func<MetadataReader, T> read)
    {
        return Pe(path, pe => read(pe.GetMetadataReader()));
    }
}
