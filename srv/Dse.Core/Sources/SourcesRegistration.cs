// Copyright (c) PNC Financial Services. All rights reserved.

using Dse.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dse.Sources;

/// <summary>Wires the source/search spine. Individual sources register their indices via their own registrations.</summary>
public sealed class SourcesRegistration : IRegistration
{
    public static void Register(IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IPrincipalResolver, ClaimsPrincipalResolver>();
        builder.Services.AddSingleton<SourceRegistry>();
        builder.Services.AddSingleton<SearchKeyFactory>();
        builder.Services.AddSingleton<ISourceSearch, SourceSearch>();
    }
}
