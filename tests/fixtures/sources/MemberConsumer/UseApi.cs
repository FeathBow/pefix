namespace MemberConsumer;

public sealed class UseApi
{
    public int Run() => MemberProvider.Api.Foo(1, "x");
}
