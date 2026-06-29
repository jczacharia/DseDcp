// Copyright (c) PNC Financial Services. All rights reserved.

using System.Runtime.CompilerServices;

namespace Dse.Extensions;

public static class AsyncExtensions
{
    /// <summary>
    ///     Wraps <paramref name="source" /> so that each pull (each call to
    ///     <c>MoveNextAsync</c>) is preceded by <paramref name="ready" />. When <paramref name="ready" />
    ///     resolves to <c>false</c>, enumeration completes — the underlying source is not touched again.
    /// </summary>
    public static async IAsyncEnumerable<T> GateBeforePull<T>(
        this IAsyncEnumerable<T> source,
        Func<CancellationToken, ValueTask<bool>> ready,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        await using var enumerator = source.GetAsyncEnumerator(ct);

        while (true)
        {
            if (!await ready(ct))
            {
                yield break;
            }

            if (!await enumerator.MoveNextAsync())
            {
                yield break;
            }

            yield return enumerator.Current;
        }
    }
}
