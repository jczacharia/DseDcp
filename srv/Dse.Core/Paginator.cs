// Copyright (c) PNC Financial Services. All rights reserved.

using System.Runtime.CompilerServices;

namespace Dse;

public sealed record Page<T>(IReadOnlyList<T> Items, string? ContinuationToken)
    where T : class;

public abstract class Paginator<T>(string initialToken) : IAsyncEnumerable<T>
    where T : class
{
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default)
    {
        await foreach (Page<T> page in AsPages(ct).ConfigureAwait(false))
        {
            foreach (T item in page.Items)
            {
                yield return item;
            }
        }
    }

    protected abstract Task<Page<T>> FetchPageAsync(string continuationToken, CancellationToken ct);

    public async IAsyncEnumerable<Page<T>> AsPages([EnumeratorCancellation] CancellationToken ct = default)
    {
        string token = initialToken;

        while (true)
        {
            Page<T> page = await FetchPageAsync(token, ct).ConfigureAwait(false);
            yield return page;

            if (page.ContinuationToken is null)
            {
                yield break;
            }

            token = page.ContinuationToken;
        }
    }
}
