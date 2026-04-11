using PeFix.Cli;
using System.Text.Json.Serialization;

namespace PeFix;

[JsonSerializable(typeof(InspectJson))]
[JsonSerializable(typeof(InspectJson[]))]
[JsonSourceGenerationOptions(WriteIndented = true, NewLine = "\n")]
internal partial class JsonContext : JsonSerializerContext
{
}
