using System;
using System.Collections.Generic;

namespace CopeSeetheMeld;

public static class Memo
{
    public static Func<TIn, TOut> Memoize<TIn, TOut>(this Func<TIn, TOut> func) where TIn : notnull
    {
        var cache = new Dictionary<TIn, TOut>();

        return input =>
        {
            if (cache.TryGetValue(input, out var cached))
                return cached;

            return cache[input] = func(input);
        };
    }
}
