// Copyright (c) PNC Financial Services. All rights reserved.

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Dse.Authentication;

namespace Dse.Tests.Core;

public sealed class UserInfoEndpointTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task UserInfoEndpointReturns401()
    {
        await using var host = new ApiHost(outputHelper);
        var response = await host.CreateClient().GetAsync("/api/userinfo", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UserInfoEndpointReturnsUser()
    {
        await using var host = new ApiHost(outputHelper);

        var response = await host.ClientWithUser("user", ["member"])
            .GetFromJsonAsync<UserInfoResponse>("/api/userinfo", TestContext.Current.CancellationToken);

        response.Should().NotBeNull();
        response.Name.Should().Be("user");
        response.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "member");
    }
}
