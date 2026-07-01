// Copyright (c) PNC Financial Services. All rights reserved.

using ServiceScan.SourceGenerator;

namespace Dse.Api.Scanning;

public static partial class RegistrationScanning
{
    [ScanForTypes(
        AssignableTo = typeof(IRegisterServices),
        Handler = nameof(IRegisterServices.Register),
        AssemblyNameFilter = "Dse.*"
    )]
    public static partial IServiceCollection AddScannedServices(this IServiceCollection services);
}
