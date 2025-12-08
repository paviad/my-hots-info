using System.Diagnostics.CodeAnalysis;

namespace MyReplayLibrary.Talents;

public static class EnumerableExtensions {
    extension<TSource>(IEnumerable<TSource> source) {
        public IEnumerable<TResult> Pairwise<TResult>(Func<TSource?, TSource, TResult> resultSelector) {
            TSource? previous = default;

            using var it = source.GetEnumerator();

            if (it.MoveNext()) {
                previous = it.Current;
            }

            while (it.MoveNext()) {
                yield return resultSelector(previous, previous = it.Current);
            }
        }

        public IEnumerable<TResult> Triplewise<TResult>(Func<TSource?, TSource?, TSource?, TResult> resultSelector) {
            TSource? a = default;
            TSource? b = default;

            using var it = source.GetEnumerator();

            if (it.MoveNext()) {
                a = it.Current;
            }

            if (it.MoveNext()) {
                b = it.Current;
            }
            else {
                yield return resultSelector(default, a, default);
            }

            yield return resultSelector(default, a, b);
            
            while (it.MoveNext()) {
                yield return resultSelector(a, b, it.Current);
                a = b;
                b = it.Current;
            }
         
            yield return resultSelector(a, b, default);
        }
    }
}
