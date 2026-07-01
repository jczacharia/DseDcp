// Copyright (c) PNC Financial Services. All rights reserved.

using Dse.Sources;

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

    public SourceKey SourceKey { get; } = SourceKey.From("confluence");

    public Type MappingContextType { get; } = typeof(ConfluenceMappingContext);

    public void Build(SourceBuilder builder) =>
        builder.AddIndex(
            IndexKey.From("content"),
            ConfluenceMappingContext.ConfluenceDoc.Context,
            PermissionPolicy.Gated(s_cflUsers)
        );
}
