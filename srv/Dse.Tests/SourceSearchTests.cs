// Copyright (c) PNC Financial Services. All rights reserved.

// using System.Security.Claims;
// using System.Text.Json.Serialization;
// using Dse.Search;
// using Dse.Sources;
// using Elastic.Clients.Elasticsearch;
// using Elastic.Mapping;
// using Elastic.Transport;
// using Microsoft.Extensions.DependencyInjection;
// using HttpMethod = Elastic.Transport.HttpMethod;
//
// namespace Dse.Tests;
//
// public sealed class TestDoc
// {
//     public string Id { get; init; } = null!;
//     public string Title { get; init; } = null!;
//     public string Space { get; init; } = null!;
// }
//
// [ElasticsearchMappingContext]
// [Index<TestDoc>(Name = "source-servicenow-incident", ReadAlias = "source-servicenow-incident")]
// public static partial class TestDocContext;
//
// /// <summary>
// ///     Functional proof of the multi-source security model through DI-resolved services against live Elasticsearch:
// ///     the gate (API-key index privilege) governs source visibility, and per-document DLS filters within a source.
// ///     Confluence is registered for real via its <c>IRegistration</c>; a ServiceNow DLS index is registered through
// ///     the host's service hook. Nothing is mocked, no client is hand-built — <see cref="ISourceSearch" /> is resolved
// ///     and it executes as the user via a minted key.
// /// </summary>
// public sealed class SourceSearchTests(ITestOutputHelper output) : IAsyncLifetime
// {
//     private const string ConfluenceIndex = "source-confluence-000001";
//     private const string ConfluenceAlias = "source-confluence-search";
//     private const string IncidentIndex = "source-servicenow-incident-000001";
//     private const string IncidentAlias = "source-servicenow-incident-search";
//
//     private const string Cfl = "GSGu_CFL_CFLUsers"; // Confluence gate group
//     private const string NetOps = "netops-group";
//     private const string SecOps = "secops-group";
//
//     private ApiHost _host = null!;
//     private ElasticsearchClient _admin = null!;
//     private ISourceSearch _search = null!;
//
//     private static CancellationToken Ct => TestContext.Current.CancellationToken;
//
//     public async ValueTask InitializeAsync()
//     {
//         // Register a ServiceNow incident index (DLS, no gate) alongside the auto-registered Confluence source.
//         _host = new(
//             output,
//             builder =>
//                 builder.ConfigureServices(services =>
//                     services.AddSingleton(
//                         new SourceIndex(
//                             SourceKey.From("servicenow"),
//                             IndexKey.From("incident"),
//                             TestDocContext.TestDoc.Context,
//                             PermissionPolicy.Dls(gateRoles: [])
//                         )
//                     )
//                 )
//         );
//
//         _admin = _host.Services.GetRequiredService<ElasticsearchClient>();
//         _search = _host.Services.GetRequiredService<ISourceSearch>();
//
//         await DropIndicesAsync();
//
//         await CreateIndexAsync(
//             ConfluenceIndex,
//             ConfluenceAlias,
//             // lang=json
//             """{"settings":{"number_of_replicas":0},"mappings":{"properties":{"title":{"type":"text"},"space":{"type":"keyword"}}}}"""
//         );
//         await CreateIndexAsync(
//             IncidentIndex,
//             IncidentAlias,
//             // lang=json
//             """{"settings":{"number_of_replicas":0},"mappings":{"properties":{"number":{"type":"keyword"},"assignment_group":{"type":"keyword"},"_allow_access_control":{"type":"keyword"}}}}"""
//         );
//
//         await IndexAsync(ConfluenceIndex, "cfl-1", new ConfluenceDoc("Employee Onboarding Guide", "HR"));
//         await IndexAsync(ConfluenceIndex, "cfl-2", new ConfluenceDoc("Corporate Travel Policy", "HR"));
//
//         await IndexAsync(IncidentIndex, "inc-1", new IncidentDoc("INC0001", "network", [NetOps]));
//         await IndexAsync(IncidentIndex, "inc-2", new IncidentDoc("INC0002", "security", [SecOps]));
//         await IndexAsync(IncidentIndex, "inc-3", new IncidentDoc("INC0003", "facilities", null));
//     }
//
//     public async ValueTask DisposeAsync()
//     {
//         await DropIndicesAsync();
//         _host.Dispose();
//     }
//
//     [Fact]
//     public async Task Gate_GovernsSourceVisibility()
//     {
//         // Holding the Confluence gate group makes its docs visible; lacking it removes them entirely.
//         var withGate = await Ids(User(Cfl), Selection.All);
//         withGate.Should().Contain(["cfl-1", "cfl-2"]);
//
//         var withoutGate = await Ids(User(), Selection.All);
//         withoutGate.Should().NotContain("cfl-1").And.NotContain("cfl-2");
//     }
//
//     [Fact]
//     public async Task Dls_FiltersWithinSourceByRole()
//     {
//         var servicenow = Selection.Sources(SourceKey.From("servicenow"));
//
//         (await Ids(User(NetOps), servicenow)).Should().BeEquivalentTo("inc-1", "inc-3"); // own + public
//         (await Ids(User(SecOps), servicenow)).Should().BeEquivalentTo("inc-2", "inc-3");
//     }
//
//     [Fact]
//     public async Task Dls_DeniesRestrictedDocsToUserWithoutRoles()
//     {
//         var servicenow = Selection.Sources(SourceKey.From("servicenow"));
//         (await Ids(User(), servicenow)).Should().BeEquivalentTo("inc-3"); // public only — deny by default
//     }
//
//     [Fact]
//     public async Task CrossSource_OneSearch_AppliesPerIndexRules()
//     {
//         // Gate-holder who is also NetOps: all Confluence + NetOps/public incidents, never the SecOps incident.
//         var hits = await Ids(User(Cfl, NetOps), Selection.All);
//         hits.Should().BeEquivalentTo("cfl-1", "cfl-2", "inc-1", "inc-3");
//     }
//
//     private async Task<string[]> Ids(ClaimsPrincipal user, Selection selection)
//     {
//         var results = await _search.SearchAsync(user, selection, null, Ct);
//         return [.. results.Select(r => r.Id)];
//     }
//
//     private static ClaimsPrincipal User(params string[] roles) =>
//         new(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "Test"));
//
//     private async Task IndexAsync<T>(string index, string id, T document) =>
//         (await _admin.IndexAsync(document, i => i.Index(index).Id(id).Refresh(Refresh.True), Ct))
//             .IsValidResponse.Should()
//             .BeTrue();
//
//     private async Task CreateIndexAsync(string index, string alias, string mappingJson)
//     {
//         (await Raw(HttpMethod.PUT, index, mappingJson)).Should().Be(200);
//         (await Raw(HttpMethod.PUT, $"{index}/_alias/{alias}", "{}")).Should().Be(200);
//     }
//
//     private async Task DropIndicesAsync() =>
//         await Raw(HttpMethod.DELETE, $"{ConfluenceIndex},{IncidentIndex}?ignore_unavailable=true&allow_no_indices=true");
//
//     private async Task<int> Raw(HttpMethod method, string path, string? body = null)
//     {
//         var response = await _admin.Transport.RequestAsync<StringResponse>(
//             new(method, path),
//             body is null ? PostData.Empty : PostData.String(body),
//             null,
//             null,
//             Ct
//         );
//         return response.ApiCallDetails.HttpStatusCode ?? 0;
//     }
//
//     private sealed record ConfluenceDoc(
//         [property: JsonPropertyName("title")] string Title,
//         [property: JsonPropertyName("space")] string Space
//     );
//
//     private sealed record IncidentDoc(
//         [property: JsonPropertyName("number")] string Number,
//         [property: JsonPropertyName("assignment_group")] string AssignmentGroup,
//         [property: JsonPropertyName("_allow_access_control")]
//         [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//             string[]? AllowAccessControl
//     );
// }
