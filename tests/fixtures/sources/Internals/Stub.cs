using System.Collections.Generic;

namespace Internals;

internal class Foo
{
    private int _bar = 42;

    internal int Get() => _bar;

    internal IEnumerable<int> Items()
    {
        yield return _bar;
    }
}
