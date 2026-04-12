using System.Text.Json.Serialization;
using PeFix.Cli;

namespace PeFix;

[JsonSerializable(typeof(InspectJson))]
[JsonSerializable(typeof(InspectJson[]))]
[JsonSerializable(typeof(CorFlagsJson))]
[JsonSerializable(typeof(SignalsJson))]
[JsonSerializable(typeof(AsmRefJson))]
[JsonSerializable(typeof(AsmRefJson[]))]
[JsonSerializable(typeof(ConflictJson))]
[JsonSerializable(typeof(ConflictJson[]))]
[JsonSerializable(typeof(FixJson))]
[JsonSerializable(typeof(RefusalJson))]
[JsonSerializable(typeof(BatchSummary))]
[JsonSerializable(typeof(BatchFixJson))]
[JsonSerializable(typeof(ScanJson))]
[JsonSerializable(typeof(SummaryJson))]
[JsonSourceGenerationOptions(WriteIndented = true, NewLine = "\n")]
internal sealed partial class JsonContext : JsonSerializerContext
{
}
