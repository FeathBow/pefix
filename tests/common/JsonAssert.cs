using System.Text.Json;

namespace PeFix.Tests;

internal static class JsonAssert
{
    public static JsonElement ParseObject(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement.Clone();
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        return root;
    }

    public static void HasProp(JsonElement obj, string name)
    {
        Assert.True(obj.TryGetProperty(name, out _), $"JSON property '{name}' was missing.");
    }

    public static JsonElement SingleBy(JsonElement array, string name, string value)
    {
        return Assert.Single(
            array.EnumerateArray(),
            item => string.Equals(item.GetProperty(name).GetString(), value, StringComparison.Ordinal));
    }

    public static string[] StringArray(JsonElement array)
    {
        return [.. array.EnumerateArray().Select(ReadString)];
    }

    private static string ReadString(JsonElement item)
    {
        return item.GetString() ?? throw new InvalidOperationException("Expected JSON string item.");
    }
}
