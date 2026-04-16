using DepStub = Dependency.Stub;
using ExtStub = FakeMsExtDi.Stub;

namespace Consumer;

public sealed class Stub
{
    public DepStub? DepField { get; set; }
    public ExtStub? ExtsField { get; set; }
}
