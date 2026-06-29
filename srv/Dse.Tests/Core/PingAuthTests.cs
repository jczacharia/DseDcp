// Copyright (c) PNC Financial Services. All rights reserved.

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Dse.Authentication;
using Moq;

namespace Dse.Tests.Core;

public sealed class PingAuthTests(ITestOutputHelper outputHelper)
{
    public static TheoryData<string[], string[]> MembershipCases =>
        new()
        {
            { [], [] },
            { ["one"], ["one"] },
            { ["a", "b"], ["a", "b"] },
            { ["a", "", "b"], ["a", "b"] }, // empty segments dropped
            { [" spaced "], ["spaced"] }, // entries trimmed
        };

    [Fact]
    public async Task NoTokenChallengesWithReauthHeader()
    {
        await using var host = new ApiHost(outputHelper);
        var response = await host.CreateClient().GetAsync("/api/userinfo", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Contains("X-Re-Auth-Required").Should().BeTrue();
        response.Headers.GetValues("X-Re-Auth-Required").Should().Equal("true");
    }

    [Fact]
    public async Task UserInfoUnresolvableFailsClosed()
    {
        await using var host = new ApiHost(outputHelper);

        host.PingAuthClientMock
            .Setup(x => x.DecodeAccessTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<string, string>?)null);

        var response = await host.ClientWithUser("user", ["member"])
            .GetAsync("/api/userinfo", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExpiredTokenIsRejectedWithoutCallingUserinfo()
    {
        await using var host = new ApiHost(outputHelper);

        var response = await host.ClientWithUser("user", ["member"], DateTime.UtcNow.AddMinutes(-1))
            .GetAsync("/api/userinfo", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        host.PingAuthClientMock.Verify(
            x => x.DecodeAccessTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task UserinfoIsResolvedEachRequestWhenCacheDisabled()
    {
        await using var host = new ApiHost(outputHelper);

        var client = host.ClientWithUser("user", ["member"]);

        await client.GetAsync("/api/userinfo", TestContext.Current.CancellationToken);
        await client.GetAsync("/api/userinfo", TestContext.Current.CancellationToken);

        host.PingAuthClientMock.Verify(
            x => x.DecodeAccessTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
    }

    [Theory]
    [MemberData(nameof(MembershipCases))]
    public async Task IsMemberOfProjectsToRoleClaims(string[] memberships, string[] expectedRoles)
    {
        await using var host = new ApiHost(outputHelper);

        var response = await host.ClientWithUser("user", memberships)
            .GetFromJsonAsync<UserInfoResponse>("/api/userinfo", TestContext.Current.CancellationToken);

        response.Should().NotBeNull();

        response
            .Claims.Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .Should()
            .BeEquivalentTo(expectedRoles);
    }

    [Fact]
    public async Task UserinfoIsResolvedOncePerTokenWhenCached()
    {
        await using var host = new ApiHost(outputHelper, [new("Ping:CacheDuration", "00:05:00")]);
        var client = host.ClientWithUser("user", ["member"]);

        await client.GetAsync("/api/userinfo", TestContext.Current.CancellationToken);
        await client.GetAsync("/api/userinfo", TestContext.Current.CancellationToken);

        host.PingAuthClientMock.Verify(
            x => x.DecodeAccessTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
