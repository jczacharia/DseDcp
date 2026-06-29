// Copyright (c) PNC Financial Services. All rights reserved.

using ServiceScan.SourceGenerator;

namespace Dse.Api.Scanning;

public static partial class RegistrationScanning
{
    [ScanForTypes(
        AssignableTo = typeof(IRegistration),
        Handler = nameof(IRegistration.Register),
        AssemblyNameFilter = "Dse.*"
    )]
    public static partial IHostApplicationBuilder AddRegistrations(this IHostApplicationBuilder services);
}
