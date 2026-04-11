using PeFix.Meta;
using System.Text.Json;

namespace PeFix.Cli;

internal static class JsonWriter
{
    public static string Render(Inspection result)
    {
        return JsonSerializer.Serialize(CreateModel(result), JsonContext.Default.InspectJson);
    }

    public static string Render(Inspection[] results)
    {
        var models = results.Select(CreateModel).ToArray();
        return JsonSerializer.Serialize(models, JsonContext.Default.InspectJsonArray);
    }

    private static InspectJson CreateModel(Inspection result)
    {
        return new InspectJson(
            result.Path,
            result.ValidPe,
            result.HasCliHeader,
            result.PeFormat,
            result.Machine,
            new CorFlagsJson(
                result.CliFlags.IlOnly,
                result.CliFlags.Required32Bit,
                result.CliFlags.Preferred32Bit,
                result.CliFlags.Signed),
            new SignalsJson(
                result.Signals.StrongName,
                result.Signals.HasPInvoke,
                result.Signals.IsRefAsm,
                result.Signals.IsMixedMode),
            result.Category is null ? null : Labels.CatText(result.Category),
            Labels.StatusText(result.Status),
            result.PrimaryCause,
            result.RuntimeRisks,
            result.Warnings,
            result.NextSteps);
    }
}
