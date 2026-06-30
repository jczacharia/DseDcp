// Copyright (c) PNC Financial Services. All rights reserved.

using Elastic.Mapping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dse.Sources;

/// <summary>
///     A source's self-contained registration. One per source; it declares the source's indices, each with its
///     mapping context, permission policy, and ingestion cycle. Discovered through DI — a source assembly ships an
///     <see cref="IRegistration" /> that calls <see cref="ServiceCollectionExtensions.AddSource" />.
/// </summary>
public interface ISourceModule
{
    public static abstract SourceKey SourceKey { get; }
    public static abstract void Build(SourceBuilder builder);
}

/// <summary>Registers a source's indices. The generic moves here, off the module, so a source owns many typed indices.</summary>
public sealed class SourceBuilder(SourceKey sourceKey, IHostApplicationBuilder hostBuilder)
{
    public SourceKey SourceKey { get; } = sourceKey;
    public IServiceCollection Services { get; } = hostBuilder.Services;
    public IConfiguration Configuration { get; } = hostBuilder.Configuration;
    public IHostEnvironment Environment { get; } = hostBuilder.Environment;

    public SourceBuilder AddIndex<TDoc>(
        IndexKey index,
        ElasticsearchTypeContext context,
        PermissionPolicy permissions,
        Cycle cycle = Cycle.Incremental
    )
        where TDoc : class
    {
        Services.AddSingleton(new SourceIndex(SourceKey, index, context.ResolveReadTarget(), permissions, cycle));
        return this;
    }
}
