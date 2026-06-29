// Copyright (c) PNC Financial Services. All rights reserved.

using System.Runtime.CompilerServices;

namespace Dse;

public sealed record Page<T>(IReadOnlyList<T> Items, string? ContinuationToken)
    where T : class;

public abstract class Paginator<T>(string initialToken) : IAsyncEnumerable<T>
    where T : class
{
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        await foreach (var page in AsPages(cancellationToken).ConfigureAwait(false))
        {
            foreach (var item in page.Items)
            {
                yield return item;
            }
        }
    }

    protected abstract Task<Page<T>> FetchPageAsync(string continuationToken, CancellationToken ct);

    public async IAsyncEnumerable<Page<T>> AsPages([EnumeratorCancellation] CancellationToken ct = default)
    {
        var token = initialToken;

        while (true)
        {
            var page = await FetchPageAsync(token, ct).ConfigureAwait(false);
            yield return page;

            if (page.ContinuationToken is null)
            {
                yield break;
            }

            token = page.ContinuationToken;
        }
    }
}
