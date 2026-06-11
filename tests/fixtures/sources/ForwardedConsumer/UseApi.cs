namespace ForwardedConsumer;

public sealed class UseApi
{
    public int Run() => ForwardedProvider.Api.Foo(3);
}
