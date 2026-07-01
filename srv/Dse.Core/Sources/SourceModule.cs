// Copyright (c) PNC Financial Services. All rights reserved.

using Elastic.Mapping;
using Microsoft.Extensions.DependencyInjection;

namespace Dse.Sources;

/// <summary>
///     A source's self-contained registration. One per source; it declares the source's indices, each with its
///     mapping context, permission policy, and ingestion cycle.
/// </summary>
public interface ISourceModule
{
    SourceKey SourceKey { get; }
    Type MappingContextType { get; }
    void Build(SourceBuilder builder);
}

/// <summary>Registers a source's indices. The generic moves here, off the module, so a source owns many typed indices.</summary>
public sealed class SourceBuilder(ISourceModule module, IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;

    public SourceBuilder AddIndex(
        IndexKey index,
        ElasticsearchTypeContext context,
        PermissionPolicy permissions,
        Cycle cycle = Cycle.Incremental
    )
    {
        Services.AddKeyedSingleton(module.SourceKey, new SourceIndex(module.SourceKey, index, context, permissions, cycle));
        return this;
    }
}
