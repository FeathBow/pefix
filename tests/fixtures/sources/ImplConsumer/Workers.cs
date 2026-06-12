namespace ImplConsumer;

public sealed class Worker : ImplProvider.IWork
{
    public int Step() => 1;
}

public sealed class Explicit : ImplProvider.IWork
{
    int ImplProvider.IWork.Step() => 2;
}

public abstract class Partial : ImplProvider.IWork
{
    public int Step() => 3;
}

public class StepBase
{
    public int Step() => 4;
}

public sealed class Derived : StepBase, ImplProvider.IWork
{
}
