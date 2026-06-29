// Copyright (c) PNC Financial Services. All rights reserved.

using System.Net.Http.Json;
using Dse.Extensions;

namespace Dse.Tests.Core;

public sealed class HealthCheckTests(ITestOutputHelper outputHelper) : ApiTest(outputHelper)
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/health/startup")]
    public async Task Health_endpoints_are_healthy(string url)
    {
        var response = await Client.GetFromJsonAsync<DseHealthReport>(url, TestContext.Current.CancellationToken);
        response.Should().BeAssignableTo<DseHealthReport>();
    }
}
