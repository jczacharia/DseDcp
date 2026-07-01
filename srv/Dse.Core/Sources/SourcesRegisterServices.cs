// Copyright (c) PNC Financial Services. All rights reserved.

using Dse.Search;
using Microsoft.Extensions.DependencyInjection;

namespace Dse.Sources;

/// <summary>Wires the source/search spine. Individual sources register their indices via their own registrations.</summary>
public sealed class SourcesRegisterServices : IRegisterServices
{
    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<IPrincipalResolver, ClaimsPrincipalResolver>();
        services.AddSingleton<SourceRegistry>();
        services.AddSingleton<SearchKeyFactory>();
        services.AddSingleton<ISourceSearch, SourceSearch>();
    }
}
