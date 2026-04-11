using PeFix.Cli;
using System.Text.Json.Serialization;

namespace PeFix;

[JsonSerializable(typeof(InspectJson))]
[JsonSerializable(typeof(InspectJson[]))]
[JsonSerializable(typeof(CorFlagsJson))]
[JsonSerializable(typeof(SignalsJson))]
[JsonSerializable(typeof(FixJson))]
[JsonSerializable(typeof(RefusalJson))]
[JsonSerializable(typeof(BatchSummary))]
[JsonSerializable(typeof(BatchFixJson))]
[JsonSourceGenerationOptions(WriteIndented = true, NewLine = "\n")]
internal partial class JsonContext : JsonSerializerContext
{
}
