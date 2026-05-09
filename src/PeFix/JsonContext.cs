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
[JsonSerializable(typeof(PinvokeJson))]
[JsonSerializable(typeof(PinvokeJson[]))]
[JsonSerializable(typeof(PinBatchJson))]
[JsonSerializable(typeof(PublicJson))]
[JsonSerializable(typeof(InspectJson))]
[JsonSerializable(typeof(InspectJson[]))]
[JsonSerializable(typeof(CorFlagsJson))]
[JsonSerializable(typeof(SignalsJson))]
[JsonSerializable(typeof(AsmRefJson))]
[JsonSerializable(typeof(AsmRefJson[]))]
[JsonSerializable(typeof(ScanConflict))]
[JsonSerializable(typeof(ScanConflict[]))]
[JsonSerializable(typeof(ScanMissing))]
[JsonSerializable(typeof(ScanMissing[]))]
[JsonSerializable(typeof(ScanDup))]
[JsonSerializable(typeof(ScanDup[]))]
[JsonSerializable(typeof(ScanIssue))]
[JsonSerializable(typeof(ScanIssue[]))]
[JsonSerializable(typeof(ScanGate))]
[JsonSerializable(typeof(FixJson))]
[JsonSerializable(typeof(RefusalJson))]
[JsonSerializable(typeof(BatchSummary))]
[JsonSerializable(typeof(BatchFixJson))]
[JsonSerializable(typeof(ScanJson))]
[JsonSerializable(typeof(ScanSummary))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(string))]
[JsonSourceGenerationOptions(WriteIndented = true, NewLine = "\n")]
internal sealed partial class JsonContext : JsonSerializerContext
{
}
