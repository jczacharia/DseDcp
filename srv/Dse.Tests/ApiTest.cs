// Copyright (c) PNC Financial Services. All rights reserved.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dse.Authentication.Ping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Dse.Tests;

public abstract class ApiTest : IAsyncLifetime
{
    private AsyncServiceScope _scope;
    private ApiHost Host { get; }
    public Mock<IPingAuthClient> PingAuthClientMock { get; }
    protected HttpClient Client => Host.CreateClient();
    protected IServiceProvider Services => _scope.ServiceProvider;
    protected ITestOutputHelper Out { get; }

    protected ApiTest(ITestOutputHelper outputHelper)
    {
        PingAuthClientMock = new Mock<IPingAuthClient>();
        PingAuthClientMock
            .Setup(x => x.DecodeAccessTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (string accessToken, CancellationToken _) =>
                {
                    var handler = new JwtSecurityTokenHandler();
                    var jwt = handler.ReadJwtToken(accessToken);
                    return jwt.Claims.ToDictionary(c => c.Type, c => c.Value);
                }
            );
        Out = outputHelper;

        Host = new ApiHost(
            Out,
            builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.Replace(ServiceDescriptor.Singleton(PingAuthClientMock.Object));
                });
            }
        );
    }

    public HttpClient ClientWithUser(string uid, string[]? memberships = null)
    {
        HttpClient client = Host.CreateClient();
        var handler = new JwtSecurityTokenHandler();
        string inner = handler.WriteToken(
            new JwtSecurityToken(
                expires: DateTime.UtcNow.AddDays(1),
                claims: [new Claim("uid", uid), new Claim("isMemberOf", string.Join("^", memberships ?? []))]
            )
        );
        string outer = handler.WriteToken(new JwtSecurityToken(claims: [new Claim("access_token", inner)]));
        client.DefaultRequestHeaders.Add("X-Ping", outer);
        return client;
    }

    public virtual ValueTask InitializeAsync()
    {
        _scope = Host.Services.CreateAsyncScope();
        return ValueTask.CompletedTask;
    }

    public virtual async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
