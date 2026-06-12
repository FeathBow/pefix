namespace AccessConsumer;

public sealed class UseApi
{
    public int Run()
    {
        return AccessProvider.Api.Open()
            + AccessProvider.Api.Hidden()
            + AccessProvider.Api.Count
            + AccessProvider.Inner.Ping();
    }
}
