// Copyright (c) PNC Financial Services. All rights reserved.

using Elastic.Mapping;
using ServiceScan.SourceGenerator;

namespace Dse.Api.Scanning;

public static partial class ElasticScanning
{
    [GenerateServiceRegistrations(
        AssignableTo = typeof(IConfigureElasticsearch<>),
        Lifetime = ServiceLifetime.Singleton,
        AssemblyNameFilter = "Dse.*"
    )]
    public static partial IServiceCollection AddScannedElasticDocConfig(this IServiceCollection services);
}
