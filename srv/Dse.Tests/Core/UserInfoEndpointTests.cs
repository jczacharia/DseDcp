// Copyright (c) PNC Financial Services. All rights reserved.

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Dse.Authentication;

namespace Dse.Tests.Core;

public sealed class UserInfoEndpointTests(ITestOutputHelper outputHelper) : ApiTest(outputHelper)
{
    [Fact]
    public async Task UserInfoEndpoint_Returns401()
    {
        var response = await Client.GetAsync("/api/userinfo", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UserInfoEndpoint_ReturnsUser()
    {
        var response = await ClientWithUser("user", ["member"])
            .GetFromJsonAsync<UserInfoResponse>("/api/userinfo", TestContext.Current.CancellationToken);
        response.Should().NotBeNull();
        response.Name.Should().Be("user");
        response.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "member");
    }
}
