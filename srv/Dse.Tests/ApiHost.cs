// Copyright (c) PNC Financial Services. All rights reserved.

using System.IdentityModel.Tokens.Jwt;
using Dse.Authentication.Ping;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;

[assembly: CaptureConsole(CaptureError = false, CaptureOut = false)]

namespace Dse.Tests;

public sealed class ApiHost : WebApplicationFactory<Program>
{
    private readonly Action<IWebHostBuilder>? _configure;
    private readonly ITestOutputHelper _outputHelper;

    public ApiHost(ITestOutputHelper outputHelper, IEnumerable<KeyValuePair<string, string?>> configurationOverrides)
        : this(outputHelper, builder => builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(configurationOverrides))) { }

    public ApiHost(ITestOutputHelper outputHelper, Action<IWebHostBuilder>? configure = null)
    {
        _outputHelper = outputHelper;
        _configure = configure;
        PingAuthClientMock = new();

        PingAuthClientMock
            .Setup(x => x.DecodeAccessTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (string accessToken, CancellationToken _) =>
                    new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims.GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g.Last().Value)
            );
    }

    public Mock<IPingAuthClient> PingAuthClientMock { get; }
    public string BaseAddress => ClientOptions.BaseAddress.ToString().TrimEnd('/');

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        UseKestrel(0);
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration(sources =>
        {
            for (var i = sources.Sources.Count - 1; i >= 0; i--)
            {
                if (sources.Sources[i] is EnvironmentVariablesConfigurationSource)
                {
                    sources.Sources.RemoveAt(i);
                }
            }
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddXUnit(_outputHelper);
        });

        builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton(PingAuthClientMock.Object)));

        _configure?.Invoke(builder);
    }

    public static ApiHost WithExtender(ITestOutputHelper outputHelper, Action<WebApplication> configure) =>
        new(outputHelper, builder => builder.ConfigureServices(services => services.AddWebAppExtender(configure)));

    public HttpClient ClientWithUser(string uid, string[]? memberships = null, DateTime? expiresUtc = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"PA.APP_DSE={BuildEnvelope()}");
        return client;

        string BuildEnvelope()
        {
            JwtSecurityTokenHandler handler = new();

            var inner = handler.WriteToken(
                new JwtSecurityToken(
                    expires: expiresUtc ?? DateTime.UtcNow.AddDays(1),
                    claims: [new("uid", uid), new("isMemberOf", string.Join('^', memberships ?? []))]
                )
            );

            return handler.WriteToken(new JwtSecurityToken(claims: [new("access_token", inner)]));
        }
    }
}
