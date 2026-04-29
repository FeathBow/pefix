using System.Text.Json.Serialization;
using PeFix.Cli;
using PeFix.Plan;

namespace PeFix;

[JsonSerializable(typeof(PefixPlan))]
[JsonSerializable(typeof(SnStripJson))]
[JsonSerializable(typeof(SnStripJson[]))]
[JsonSerializable(typeof(SnBatchJson))]
[JsonSerializable(typeof(SnDepJson))]
[JsonSerializable(typeof(SnDepJson[]))]
[JsonSerializable(typeof(RedirJson))]
[JsonSerializable(typeof(RedirJson[]))]
[JsonSerializable(typeof(RedBatchJson))]
[JsonSerializable(typeof(InspectJson))]
[JsonSerializable(typeof(InspectJson[]))]
[JsonSerializable(typeof(CorFlagsJson))]
[JsonSerializable(typeof(SignalsJson))]
[JsonSerializable(typeof(AsmRefJson))]
[JsonSerializable(typeof(AsmRefJson[]))]
[JsonSerializable(typeof(ConflictJson))]
[JsonSerializable(typeof(ConflictJson[]))]
[JsonSerializable(typeof(MissRefJson))]
[JsonSerializable(typeof(MissRefJson[]))]
[JsonSerializable(typeof(DupJson))]
[JsonSerializable(typeof(DupJson[]))]
[JsonSerializable(typeof(FixJson))]
[JsonSerializable(typeof(RefusalJson))]
[JsonSerializable(typeof(BatchSummary))]
[JsonSerializable(typeof(BatchFixJson))]
[JsonSerializable(typeof(ScanJson))]
[JsonSerializable(typeof(SummaryJson))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(string))]
[JsonSourceGenerationOptions(WriteIndented = true, NewLine = "\n")]
internal sealed partial class JsonContext : JsonSerializerContext
{
}
