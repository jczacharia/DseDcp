// Copyright (c) PNC Financial Services. All rights reserved.

using System.Security.Claims;
using System.Text.Json;
using Dse.Sources;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Caching.Memory;
using HttpMethod = Elastic.Transport.HttpMethod;

namespace Dse.Search;

/// <summary>
///     Mints (and caches) the short-lived Elasticsearch API key that carries a user's authorization. One role
///     descriptor per index the user's principals can read — the index <em>read</em> privilege is the gate, the
///     optional embedded query is per-document DLS. The key, not Dse.Api, is what Elasticsearch enforces.
/// </summary>
public sealed class SearchKeyFactory(ElasticsearchClient admin, SourceRegistry registry, IMemoryCache cache)
{
    private static readonly TimeSpan KeyLifetime = TimeSpan.FromMinutes(20);

    public async ValueTask<string> GetOrMintAsync(ClaimsPrincipal user, IReadOnlySet<Principal> principals, CancellationToken ct)
    {
        // Principals fully determine the descriptors and the DLS terms, so they are the entire cache key.
        var cacheKey = $"dse-key:{string.Join('|', principals.Select(p => p.Value).Order())}";

        return (
            await cache.GetOrCreateAsync(
                cacheKey,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = KeyLifetime;
                    return await MintAsync(user, principals, ct);
                }
            )
        )!;
    }

    private async Task<string> MintAsync(ClaimsPrincipal user, IReadOnlySet<Principal> principals, CancellationToken ct)
    {
        var roleDescriptors = new Dictionary<string, object>();

        foreach (var index in registry.ReadableBy(principals))
        {
            var privilege = new Dictionary<string, object>
            {
                ["names"] = new[] { index.ReadTarget },
                ["privileges"] = new[] { "read" },
            };

            if (index.Permissions.BuildDlsQuery(principals) is { } dls)
            {
                privilege["query"] = dls;
            }

            roleDescriptors[index.Key] = new { indices = new[] { privilege } };
        }

        // An empty role_descriptors would inherit the creator's (broad) privileges — deny by default instead.
        if (roleDescriptors.Count == 0)
        {
            roleDescriptors["__deny__"] = new
            {
                indices = new[] { new { names = new[] { "dse-deny-none" }, privileges = new[] { "read" } } },
            };
        }

        var body = JsonSerializer.Serialize(
            new
            {
                name = $"dse-{user.Identity?.Name ?? "anonymous"}",
                expiration = "20m",
                role_descriptors = roleDescriptors,
            }
        );

        var response = await admin.Transport.RequestAsync<StringResponse>(
            new EndpointPath(HttpMethod.POST, "/_security/api_key"),
            PostData.String(body),
            null,
            null,
            ct
        );

        if (response.ApiCallDetails.HttpStatusCode != 200)
        {
            throw new InvalidOperationException(
                $"Failed to mint search API key: {response.ApiCallDetails.HttpStatusCode} {response.Body}"
            );
        }

        using var json = JsonDocument.Parse(response.Body);
        return json.RootElement.GetProperty("encoded").GetString()
            ?? throw new InvalidOperationException("API key response missing 'encoded'.");
    }
}
