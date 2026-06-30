// Copyright (c) PNC Financial Services. All rights reserved.

using Dse.Sources;
using Microsoft.Extensions.Hosting;

namespace Dse.Confluence;

/// <summary>
///     Confluence — a single index, gated by the CFL users group. Public-within-gate today (no per-document DLS);
///     the degenerate case of the multi-index model.
/// </summary>
public sealed class ConfluenceModule : ISourceModule
{
    /// The AD group every Confluence document requires. Short token here matches the claims-only resolver;
    /// the LDAP resolver will yield the full DN (CN=GSGu_CFL_CFLUsers,OU=OUg_Applications,DC=pncbank,DC=com).
    private static readonly Principal s_cflUsers = Principal.From("GSGu_CFL_CFLUsers");

    public static SourceKey SourceKey { get; } = SourceKey.From("confluence");

    public static void Build(SourceBuilder builder) =>
        builder.AddIndex<ConfluenceDoc>(
            IndexKey.From("content"),
            ConfluenceContext.ConfluenceDoc.Context,
            PermissionPolicy.Gated(s_cflUsers)
        );
}

public sealed class ConfluenceRegistration : IRegistration
{
    public static void Register(IHostApplicationBuilder builder) => builder.Services.AddSource(new ConfluenceModule());
}
