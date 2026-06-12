namespace PeFix.Meta;

internal readonly record struct ReflRef(
    string ConsumerPath,
    string ReferenceName,
    string SinkType,
    string SinkMethod,
    bool AdvisoryOnly,
    bool StaticCtor);
