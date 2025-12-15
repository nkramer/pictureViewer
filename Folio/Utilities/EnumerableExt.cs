using System;
using System.Collections.Generic;
using System.Linq;

namespace Folio.Utilities;
public static class EnumerableExt {
    public static IEnumerable<IEnumerable<T>> Partition<T>(this IEnumerable<T> input, Func<T, bool> test) {
        var enumerator = input.GetEnumerator();

        while (enumerator.MoveNext()) {
            yield return nextPartition(enumerator, test);
        }
    }

    private static IEnumerable<T> nextPartition<T>(IEnumerator<T> enumerator, Func<T, bool> test) {
        do {
            yield return enumerator.Current;
        }
        while (!test(enumerator.Current));
    }

    public static IEnumerable<IEnumerable<T>> SplitBeforeIf<T>(
        this IEnumerable<T> source, Func<T, bool> predicate) {
        var temp = new List<T>();

        foreach (var item in source)
            if (predicate(item)) {
                if (temp.Any())
                    yield return temp;

                temp = new List<T> { item };
            } else
                temp.Add(item);

        yield return temp;
    }
}
