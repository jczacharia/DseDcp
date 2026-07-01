// Copyright (c) PNC Financial Services. All rights reserved.

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dse.Authentication.Ping;

public sealed class PingAuthRegisterServices : IRegisterServices
{
    public static void Register(IServiceCollection services)
    {
        services
            .AddAuthentication()
            .AddScheme<PingAuthOptions, PingAuthHandler>(PingAuthDefaults.AuthenticationScheme, static _ => { });

        services.AddMemoryCache();
        services.AddTransient<IPingAuthClient, PingAuthClient>();

        services
            .AddHttpClient(PingAuthDefaults.HttpClientName)
            .ConfigureHttpClient(
                static (sp, client) =>
                {
                    var options = sp.GetRequiredService<IOptionsMonitor<PingAuthOptions>>()
                        .Get(PingAuthDefaults.AuthenticationScheme);

                    client.BaseAddress = new(options.BaseAddress);
                }
            )
            .ConfigurePrimaryHttpMessageHandler(static sp =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<PingAuthOptions>>()
                    .Get(PingAuthDefaults.AuthenticationScheme);

                var useProxy = !string.IsNullOrWhiteSpace(options.ProxyAddress);

                return new HttpClientHandler
                {
                    Proxy = useProxy ? new WebProxy(options.ProxyAddress) : null,
                    UseProxy = useProxy,
                };
            })
            .AddStandardResilienceHandler();
    }
}
