using PeFix.Meta;

namespace PeFix.Tests;

[Trait("Category", "Unit")]
public sealed class RefFilterTests
{
    [Theory]
    [InlineData("mscorlib")]
    [InlineData("System")]
    [InlineData("UnityEngine")]
    [InlineData("netstandard")]
    [InlineData("Microsoft.CSharp")]
    [InlineData("System.Text.Json")]
    [InlineData("System.Runtime")]
    [InlineData("WindowsBase")]
    [InlineData("0Harmony")]
    [InlineData("Harmony")]
    [InlineData("GodotSharp")]
    [InlineData("BepInEx")]
    [InlineData("BepInEx.Core")]
    [InlineData("MelonLoader")]
    [InlineData("UnityEngine.UI")]
    [InlineData("UnityEditor.Build")]
    [InlineData("MSCORLIB")]
    [InlineData("system.text.json")]
    [InlineData("godotSHARP")]
    public void Provided_Yes(string name)
    {
        Assert.True(RefFilter.IsProvided(name));
    }

    [Theory]
    [InlineData("MyMod")]
    [InlineData("EnderLilis")]
    [InlineData("MyGame")]
    [InlineData("CustomGameLib")]
    [InlineData("Newtonsoft.Json")]
    public void Provided_No(string name)
    {
        Assert.False(RefFilter.IsProvided(name));
    }

    [Theory]
    [InlineData("System.Text.Json", "Framework")]
    [InlineData("UnityEngine.UI", "Host")]
    [InlineData("BepInEx.Core", "Loader")]
    [InlineData("MelonLoader", "Loader")]
    [InlineData("Newtonsoft.Json", "None")]
    public void Classify_ReturnsPolicyKind(string name, string expected)
    {
        Assert.Equal(expected, RefFilter.Classify(name).ToString());
    }

    [Fact]
    public void HostProfile_ControlsProvidedLeafRules()
    {
        HostProfile hostProfile = new(
            "test-host",
            new Dictionary<string, ProvidedKind>(StringComparer.OrdinalIgnoreCase)
            {
                ["HostOnly"] = ProvidedKind.Host,
            },
            []);

        Assert.True(RefFilter.IsProvided("hostonly", hostProfile));
        Assert.False(RefFilter.IsProvided("System", hostProfile));
    }
}
