// Copyright (c) PNC Financial Services. All rights reserved.

using ServiceScan.SourceGenerator;

namespace Dse.Api.Scanning;

public static partial class EndpointScanning
{
    [ScanForTypes(AssignableTo = typeof(IEndpoint), Handler = nameof(IEndpoint.MapEndpoint), AssemblyNameFilter = "Dse.*")]
    public static partial IEndpointRouteBuilder MapScannedEndpoints(this IEndpointRouteBuilder endpoints);
}
