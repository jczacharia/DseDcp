// Copyright (c) PNC Financial Services. All rights reserved.

using System.Collections.Frozen;

namespace Dse.Sources;

/// <summary>A user's choice of what to search: every source, or a named subset.</summary>
public sealed class Selection
{
    private readonly IReadOnlySet<SourceKey>? _sources;

    private Selection(IReadOnlySet<SourceKey>? sources) => _sources = sources;

    public static Selection All { get; } = new(null);

    public static Selection Sources(params SourceKey[] sources) => new(sources.ToFrozenSet());

    public bool Includes(SourceIndex index) => _sources is null || _sources.Contains(index.Source);
}

/// <summary>
///     The set of registered <see cref="SourceIndex" />, enumerated from DI. Resolves a <see cref="Selection" /> to
///     concrete indices and filters them by what a user's principals can read.
/// </summary>
public sealed class SourceRegistry(IEnumerable<SourceIndex> indices)
{
    public IReadOnlyList<SourceIndex> Indices { get; } = [.. indices];

    public IEnumerable<SourceIndex> ReadableBy(IReadOnlySet<Principal> principals) =>
        Indices.Where(i => i.Permissions.GatePasses(principals));

    public IEnumerable<SourceIndex> Resolve(Selection selection, IReadOnlySet<Principal> principals) =>
        Indices.Where(i => selection.Includes(i) && i.Permissions.GatePasses(principals));
}
