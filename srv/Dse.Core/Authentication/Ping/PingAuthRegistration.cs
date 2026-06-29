// Copyright (c) PNC Financial Services. All rights reserved.

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dse.Authentication.Ping;

public sealed class PingAuthRegistration : IRegistration
{
    public static void Register(IHostApplicationBuilder builder)
    {
        _ = builder
            .Services.AddAuthentication()
            .AddScheme<PingAuthOptions, PingAuthHandler>(PingAuthDefaults.AuthenticationScheme, static _ => { });

        _ = builder.Services.AddMemoryCache();

        _ = builder
            .Services.AddHttpClient(PingAuthDefaults.HttpClientName)
            .ConfigureHttpClient(
                static (sp, client) =>
                {
                    PingAuthOptions options = sp.GetRequiredService<IOptionsMonitor<PingAuthOptions>>()
                        .Get(PingAuthDefaults.AuthenticationScheme);
                    client.BaseAddress = new(options.BaseAddress);
                }
            )
            .ConfigurePrimaryHttpMessageHandler(static sp =>
            {
                PingAuthOptions options = sp.GetRequiredService<IOptionsMonitor<PingAuthOptions>>()
                    .Get(PingAuthDefaults.AuthenticationScheme);
                bool useProxy = !string.IsNullOrWhiteSpace(options.ProxyAddress);
                return new HttpClientHandler
                {
                    Proxy = useProxy ? new WebProxy(options.ProxyAddress) : null,
                    UseProxy = useProxy,
                };
            })
            .AddStandardResilienceHandler();
    }
}
