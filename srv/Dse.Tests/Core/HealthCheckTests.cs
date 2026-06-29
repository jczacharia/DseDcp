// Copyright (c) PNC Financial Services. All rights reserved.

using System.Net.Http.Json;
using Dse.Extensions;

namespace Dse.Tests.Core;

public sealed class HealthCheckTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [InlineData("/api/health")]
    [InlineData("/api/health/live")]
    [InlineData("/api/health/ready")]
    [InlineData("/api/health/startup")]
    public async Task HealthEndpointsAreHealthy(string url)
    {
        await using var host = new ApiHost(outputHelper);
        var response = await host.CreateClient().GetFromJsonAsync<DseHealthReport>(url, TestContext.Current.CancellationToken);
        response.Should().BeAssignableTo<DseHealthReport>();
    }
}
