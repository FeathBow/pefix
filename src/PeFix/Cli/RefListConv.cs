using System.Text.Json;
using System.Text.Json.Serialization;
using PeFix.Meta;

namespace PeFix.Cli;

internal sealed class RefListConv : JsonConverter<RefFinding[]>
{
    public override RefFinding[] Read(
        ref Utf8JsonReader reader,
        Type type,
        JsonSerializerOptions opts)
    {
        throw new NotSupportedException("Scan reference rows are write-only.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        RefFinding[] value,
        JsonSerializerOptions opts)
    {
        writer.WriteStartArray();
        foreach (RefFinding find in value)
            WriteRow(writer, find);

        writer.WriteEndArray();
    }

    private static void WriteRow(Utf8JsonWriter writer, RefFinding find)
    {
        writer.WriteStartObject();
        switch (find.Resolution)
        {
            case RefOutcome.VersionConflict:
                WriteCon(writer, find);
                break;
            case RefOutcome.Missing:
                WriteMiss(writer, find);
                break;
            case RefOutcome.DuplicateProvider:
                WriteDup(writer, find);
                break;
            default:
                throw new InvalidOperationException($"Unsupported scan row outcome '{find.Resolution}'.");
        }

        writer.WriteEndObject();
    }

    private static void WriteCon(Utf8JsonWriter writer, RefFinding find)
    {
        writer.WriteString("assembly", find.ReferenceName);
        writer.WriteString("expected", find.ExpectedVersion!);
        writer.WriteString("actual", find.ActualVersion!);
        writer.WriteString("referenced_by", find.ConsumerPath);
        writer.WriteString("provided_by", find.ProviderPath!);
    }

    private static void WriteMiss(Utf8JsonWriter writer, RefFinding find)
    {
        writer.WriteString("assembly", find.ReferenceName);
        writer.WriteString("version", find.ExpectedVersion!);
        writer.WriteString("required_by", find.ConsumerPath);
    }

    private static void WriteDup(Utf8JsonWriter writer, RefFinding find)
    {
        writer.WriteString("assembly", find.ReferenceName);
        writer.WritePropertyName("files");
        WriteFiles(writer, find.ProviderPaths!);
    }

    private static void WriteFiles(Utf8JsonWriter writer, string[] paths)
    {
        writer.WriteStartArray();
        foreach (string path in paths)
            writer.WriteStringValue(path);

        writer.WriteEndArray();
    }
}
