using PeFix.Patch;

namespace PeFix.Tests;

[Trait("Category", "Integration")]
public sealed class PinvokeTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void HasCalls()
    {
        string path = _temp.Copy("F04_x64_pinvoke.dll");
        PinvokeResult r = PinvokeScan.Inspect(path);
        Assert.NotEmpty(r.Calls);
        Assert.Contains(r.Calls, c => c.MethodName == "Invoke");
    }

    [Fact]
    public void ByModule()
    {
        string path = _temp.Copy("F04_x64_pinvoke.dll");
        PinvokeResult r = PinvokeScan.Inspect(path);
        Assert.Contains(r.Calls, c => string.Equals(c.Module, "native", StringComparison.Ordinal));
    }

    [Fact]
    public void NoCalls()
    {
        string path = _temp.Copy("F01_compatible_anycpu.dll");
        PinvokeResult r = PinvokeScan.Inspect(path);
        Assert.Empty(r.Calls);
    }

    [Fact]
    public void DirCalls()
    {
        _temp.Copy("F04_x64_pinvoke.dll");
        _temp.Copy("F01_compatible_anycpu.dll");
        PinBatch batch = PinvokeScan.InspectDir(_temp.DirPath);
        Assert.Single(batch.Results);
        Assert.Empty(batch.Refusals);
        Assert.EndsWith("F04_x64_pinvoke.dll", batch.Results[0].Path);
    }

    [Fact]
    public void DirRefs()
    {
        _temp.Copy("F04_x64_pinvoke.dll");
        _temp.Copy("F07_native_pe.dll");
        PinBatch batch = PinvokeScan.InspectDir(_temp.DirPath);
        Assert.Single(batch.Results);
        Assert.Single(batch.Refusals);
        Assert.EndsWith("F07_native_pe.dll", batch.Refusals[0].Path);
    }

    [Fact]
    public void NoCli()
    {
        string path = _temp.Copy("F07_native_pe.dll");
        Assert.Throws<RefusalException>(() => PinvokeScan.Inspect(path));
    }
}
