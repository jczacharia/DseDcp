// Copyright (c) PNC Financial Services. All rights reserved.

using Elastic.Mapping;

namespace Dse.Sources;

public enum Cycle
{
    /// <summary>Content-hash-gated upsert into a live index. The default; Confluence's model.</summary>
    Incremental,

    /// <summary>Full reload into a fresh index then an atomic read-alias swap.</summary>
    RebuildSwap,
}

/// <summary>
///     One searchable index contributed by a source: its read target, how it is secured, and (for ingestion) its
///     cycle. The registry's atom — a source registers one of these per index it owns.
/// </summary>
public sealed record SourceIndex(
    SourceKey Source,
    IndexKey Index,
    ElasticsearchTypeContext Context,
    PermissionPolicy Permissions,
    Cycle Cycle = Cycle.Incremental
)
{
    /// <summary>Stable composite identifier, used as the API key role-descriptor name.</summary>
    public string Key => $"{Source}-{Index}";
}
