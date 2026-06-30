// Copyright (c) PNC Financial Services. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;
using Dse.Search;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.HealthReport;
using Elastic.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HttpMethod = Elastic.Transport.HttpMethod;

namespace Dse.Tests;

/// <summary>
///     Spikes proving the multi-source security model against a live (trial-licensed) Elasticsearch:
///     the per-source <em>gate</em> is an API-key index privilege, and per-document DLS is the role-descriptor
///     <c>query</c>. Both are minted by the admin (proxy) identity and enforced by Elasticsearch when the
///     search runs under the user's short-lived API key — Dse.Api carries no filtering logic of its own.
///
///     Note: DLS (the role-descriptor <c>query</c>) is enforced only on Platinum/Enterprise (or trial).
///     The gate (index privilege) works on every tier including basic.
/// </summary>
public sealed class SpikeTests(ITestOutputHelper output) : IAsyncLifetime
{
    private const string ConfluenceIndex = "source-confluence-content-000001";
    private const string ConfluenceAlias = "source-confluence-content-search";
    private const string ServiceNowIndex = "source-servicenow-incident-000001";
    private const string ServiceNowAlias = "source-servicenow-incident-search";
    private const string AllSearchPattern = "source-*-search";

    private const string NetOps = "CN=GSGu_NetOps,OU=OUg_Applications,DC=pncbank,DC=com";
    private const string SecOps = "CN=GSGu_SecOps,OU=OUg_Applications,DC=pncbank,DC=com";

    // Public-within-gate: a doc with no _allow_access_control is visible to anyone past the gate.
    // Restricted: terms-match against the user's resolved group DNs.
    private const string DlsSource =
        """{"bool":{"should":[{"bool":{"must_not":{"exists":{"field":"_allow_access_control"}}}},{"terms":{"_allow_access_control":{{#toJson}}access{{/toJson}}}}],"minimum_should_match":1}}""";

    private ApiHost _host = null!;
    private ElasticsearchClient _admin = null!;
    private Uri _baseAddress = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _host = new ApiHost(output);
        _admin = _host.Services.GetRequiredService<ElasticsearchClient>();
        _baseAddress = new Uri(_host.Services.GetRequiredService<IOptions<ElasticOptions>>().Value.BaseAddress);

        await DropIndicesAsync();

        await CreateIndexAsync(
            ConfluenceIndex,
            ConfluenceAlias,
            """{"settings":{"number_of_replicas":0},"mappings":{"properties":{"title":{"type":"text"},"body":{"type":"text"},"space":{"type":"keyword"},"_allow_access_control":{"type":"keyword"}}}}"""
        );
        await CreateIndexAsync(
            ServiceNowIndex,
            ServiceNowAlias,
            """{"settings":{"number_of_replicas":0},"mappings":{"properties":{"number":{"type":"keyword"},"short_description":{"type":"text"},"assignment_group":{"type":"keyword"},"_allow_access_control":{"type":"keyword"}}}}"""
        );

        // Confluence today: public-within-gate, so no _allow_access_control on any doc.
        await IndexAsync(ConfluenceIndex, "cfl-1", new ConfluenceDoc("Employee Onboarding Guide", "Welcome to PNC.", "HR"));
        await IndexAsync(ConfluenceIndex, "cfl-2", new ConfluenceDoc("Corporate Travel Policy", "Booking and expenses.", "HR"));

        // ServiceNow: per-doc ACLs, plus one public incident.
        await IndexAsync(ServiceNowIndex, "inc-1", new IncidentDoc("INC0001", "VPN outage", "network", [NetOps]));
        await IndexAsync(ServiceNowIndex, "inc-2", new IncidentDoc("INC0002", "Phishing campaign", "security", [SecOps]));
        await IndexAsync(ServiceNowIndex, "inc-3", new IncidentDoc("INC0003", "Cafeteria wifi slow", "facilities", null));
    }

    public async ValueTask DisposeAsync()
    {
        await DropIndicesAsync();
        _host.Dispose();
    }

    [Fact]
    public async Task ClusterIsReachable()
    {
        var health = await _admin.HealthReportAsync(Ct);
        health.Status.Should().Be(IndicatorHealthStatus.Green);
    }

    [Fact]
    public async Task Gate_ApiKeyIndexPrivilege_GovernsSourceVisibility()
    {
        // Key carries only the Confluence descriptor — the user holds the gate group for Confluence but not ServiceNow.
        var user = UserClient(await MintKeyAsync("gate-confluence-only", new() { ["confluence"] = ConfluenceRole() }));

        // Wildcard search across all sources silently excludes indices the key has no privilege on.
        var visible = await SearchIdsAsync(user, AllSearchPattern);
        visible.Should().BeEquivalentTo("cfl-1", "cfl-2");

        // Targeting the un-granted index explicitly is a hard 403 — the gate is real, not advisory.
        var denied = await user.SearchAsync<JsonElement>(ServiceNowAlias, s => s.Query(q => q.MatchAll()), Ct);
        denied.IsValidResponse.Should().BeFalse();
        denied.ApiCallDetails.HttpStatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Dls_Query_FiltersDocumentsByUserRoles()
    {
        var netOps = UserClient(await MintKeyAsync("dls-netops", new() { ["servicenow"] = ServiceNowRole([NetOps]) }));
        (await SearchIdsAsync(netOps, ServiceNowAlias)).Should().BeEquivalentTo("inc-1", "inc-3"); // own + public, not SecOps

        var secOps = UserClient(await MintKeyAsync("dls-secops", new() { ["servicenow"] = ServiceNowRole([SecOps]) }));
        (await SearchIdsAsync(secOps, ServiceNowAlias)).Should().BeEquivalentTo("inc-2", "inc-3"); // own + public, not NetOps
    }

    [Fact]
    public async Task Dls_Query_DeniesRestrictedDocsToUserWithoutRoles()
    {
        // No matching group DNs → terms matches nothing; only the public (no-ACL) doc survives. Deny by default.
        var noRoles = UserClient(await MintKeyAsync("dls-no-roles", new() { ["servicenow"] = ServiceNowRole([]) }));
        (await SearchIdsAsync(noRoles, ServiceNowAlias)).Should().BeEquivalentTo("inc-3");
    }

    [Fact]
    public async Task CrossSource_SingleApiKey_AppliesPerIndexRulesInOneSearch()
    {
        // One key, two descriptors: Confluence is gate-only (public-within-gate); ServiceNow is DLS-filtered to NetOps.
        var user = UserClient(
            await MintKeyAsync(
                "cross-source",
                new() { ["confluence"] = ConfluenceRole(), ["servicenow"] = ServiceNowRole([NetOps]) }
            )
        );

        // A single wildcard search returns the union, each index filtered by its own model — the whole design in one call.
        var hits = await SearchIdsAsync(user, AllSearchPattern);
        hits.Should().BeEquivalentTo("cfl-1", "cfl-2", "inc-1", "inc-3"); // all Confluence + NetOps/public incidents, never inc-2
    }

    // --- role descriptor builders (serialized verbatim; the proxy will assemble these from resolved group DNs) ---

    private static object ConfluenceRole() =>
        new { indices = new[] { new { names = new[] { ConfluenceAlias }, privileges = new[] { "read" } } } };

    private static object ServiceNowRole(string[] access) =>
        new
        {
            indices = new[]
            {
                new
                {
                    names = new[] { ServiceNowAlias },
                    privileges = new[] { "read" },
                    query = new { template = new { @params = new { access }, source = DlsSource } },
                },
            },
        };

    // --- elasticsearch plumbing ---

    private async Task<string> MintKeyAsync(string name, Dictionary<string, object> roleDescriptors)
    {
        // Serialized with plain STJ so property names ("role_descriptors", "params") survive verbatim — the typed
        // CreateApiKey model can't express the mustache-templated DLS query (IndicesPrivileges.Query is object?).
        var body = JsonSerializer.Serialize(new { name, expiration = "1h", role_descriptors = roleDescriptors });
        var response = await _admin.Transport.RequestAsync<StringResponse>(
            new EndpointPath(HttpMethod.POST, "/_security/api_key"),
            PostData.String(body),
            null,
            null,
            Ct
        );

        response.ApiCallDetails.HttpStatusCode.Should().Be(200, response.Body);
        using var json = JsonDocument.Parse(response.Body);
        return json.RootElement.GetProperty("encoded").GetString()!;
    }

    private ElasticsearchClient UserClient(string encodedApiKey) =>
        new(new ElasticsearchClientSettings(_baseAddress).Authentication(new ApiKey(encodedApiKey)));

    private async Task<string[]> SearchIdsAsync(ElasticsearchClient client, Indices target)
    {
        var response = await client.SearchAsync<JsonElement>(target, s => s.Size(50).Query(q => q.MatchAll()), Ct);
        response.IsValidResponse.Should().BeTrue();
        return [.. response.Hits.Select(h => h.Id!)];
    }

    private async Task IndexAsync<T>(string index, string id, T document) =>
        (await _admin.IndexAsync(document, i => i.Index(index).Id(id).Refresh(Refresh.True), Ct))
            .IsValidResponse.Should()
            .BeTrue();

    private async Task CreateIndexAsync(string index, string alias, string mappingJson)
    {
        (await Raw(HttpMethod.PUT, index, mappingJson)).HttpStatusCode.Should().Be(200);
        (await Raw(HttpMethod.PUT, $"{index}/_alias/{alias}", "{}")).HttpStatusCode.Should().Be(200);
    }

    private async Task DropIndicesAsync() =>
        await Raw(HttpMethod.DELETE, $"{ConfluenceIndex},{ServiceNowIndex}?ignore_unavailable=true&allow_no_indices=true");

    private async Task<ApiCallDetails> Raw(HttpMethod method, string path, string? body = null)
    {
        var response = await _admin.Transport.RequestAsync<StringResponse>(
            new EndpointPath(method, path),
            body is null ? PostData.Empty : PostData.String(body),
            null,
            null,
            Ct
        );
        return response.ApiCallDetails;
    }

    private sealed record ConfluenceDoc(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("space")] string Space
    );

    private sealed record IncidentDoc(
        [property: JsonPropertyName("number")] string Number,
        [property: JsonPropertyName("short_description")] string ShortDescription,
        [property: JsonPropertyName("assignment_group")] string AssignmentGroup,
        [property: JsonPropertyName("_allow_access_control")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string[]? AllowAccessControl
    );
}
