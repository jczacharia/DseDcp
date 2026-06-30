// Copyright (c) PNC Financial Services. All rights reserved.

using System.Security.Claims;
using System.Text.Json;
using Dse.Sources;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

namespace Dse.Search;

public sealed record SearchResult(string Index, string Id);

/// <summary>
///     Executes a user's search across the selected sources. The user's authorization travels in a minted API key;
///     Elasticsearch enforces it. Dse.Api holds no filtering logic.
/// </summary>
public interface ISourceSearch
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        ClaimsPrincipal user,
        Selection selection,
        string? query,
        CancellationToken ct = default
    );
}

public sealed class SourceSearch(
    ElasticsearchClient client,
    IPrincipalResolver resolver,
    SourceRegistry registry,
    SearchKeyFactory keyFactory
) : ISourceSearch
{
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        ClaimsPrincipal user,
        Selection selection,
        string? query,
        CancellationToken ct = default
    )
    {
        var principals = await resolver.ResolveAsync(user, ct);

        // Target only readable indices in the selection — explicitly naming an un-granted index is a 403.
        var targets = registry.Resolve(selection, principals).Select(i => i.ReadTarget).ToArray();
        if (targets.Length == 0)
        {
            return [];
        }

        var apiKey = await keyFactory.GetOrMintAsync(user, principals, ct);

        // Execute as the user by overriding auth for this one request on the injected (admin) client.
        var response = await client.SearchAsync<JsonElement>(
            string.Join(',', targets),
            s =>
            {
                s.RequestConfiguration(rc => rc.Authentication(new ApiKey(apiKey)));
                s.Size(100);
                s.Query(q =>
                {
                    if (string.IsNullOrWhiteSpace(query))
                    {
                        q.MatchAll();
                    }
                    else
                    {
                        q.SimpleQueryString(sqs =>
                            sqs.Query(query).DefaultOperator(Elastic.Clients.Elasticsearch.QueryDsl.Operator.And)
                        );
                    }
                });
            },
            ct
        );

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException(
                $"Search failed: {response.ApiCallDetails.HttpStatusCode} {response.ApiCallDetails.OriginalException?.Message}"
            );
        }

        return [.. response.Hits.Select(h => new SearchResult(h.Index!, h.Id!))];
    }
}
