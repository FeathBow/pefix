namespace PeFix.Meta;

internal readonly record struct ReflScan(
    ReflRef[] References,
    bool HasCustomResolver,
    int DesyncMethodCount);
