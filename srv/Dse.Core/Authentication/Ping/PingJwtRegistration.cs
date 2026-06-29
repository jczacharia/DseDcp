// Copyright (c) PNC Financial Services. All rights reserved.

using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Dse.Authentication.Ping;

public sealed class PingJwtRegistration : IRegistration
{
    public static void Register(IHostApplicationBuilder builder)
    {
        builder
            .Services.AddHttpClient(PingJwtDefaults.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                PingJwtOptions options = sp.GetRequiredService<IOptions<PingJwtOptions>>().Value;
                bool useProxy = !string.IsNullOrWhiteSpace(options.ProxyAddress);
                return new HttpClientHandler
                {
                    Proxy = useProxy ? new WebProxy(options.ProxyAddress) : null,
                    UseProxy = useProxy,
                };
            })
            .AddStandardResilienceHandler();

        builder.Services.AddTransient<JwksConfigurationRetriever>();
        builder.Services.AddAuthentication().AddJwtBearer(PingJwtDefaults.AuthenticationScheme);

        builder
            .Services.AddOptions<JwtBearerOptions>(PingJwtDefaults.AuthenticationScheme)
            .BindConfiguration(PingJwtDefaults.OptionsPath)
            .Configure<IServiceProvider>(
                (jwt, sp) =>
                {
                    var ping = sp.GetRequiredService<IOptions<PingJwtOptions>>().Value;
                    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();

                    jwt.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                        jwt.MetadataAddress,
                        sp.GetRequiredService<JwksConfigurationRetriever>(),
                        new HttpDocumentRetriever(httpClientFactory.CreateClient(PingJwtDefaults.HttpClientName))
                    );

                    jwt.TokenValidationParameters.TransformBeforeSignatureValidation = (token, _) =>
                    {
                        if (
                            token is JsonWebToken outer
                            && outer.TryGetPayloadValue<string>(ping.AccessTokenClaim, out string? inner)
                            && !string.IsNullOrEmpty(inner)
                        )
                        {
                            return new JsonWebToken(inner);
                        }

                        return token;
                    };

                    jwt.Events = new JwtBearerEvents
                    {
                        // The gateway delivers the JWT in the PA.* cookie; fall back to a header if one is configured.
                        OnMessageReceived = context =>
                        {
                            if (
                                context.Request.Cookies.TryGetValue(ping.CookieName, out string? cookie)
                                && !string.IsNullOrEmpty(cookie)
                            )
                            {
                                context.Token = cookie;
                            }
                            else if (context.HttpContext.Request.Headers.Authorization.FirstOrDefault() is { } raw)
                            {
                                string token = raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                                    ? raw["Bearer ".Length..].Trim()
                                    : raw;

                                if (!string.IsNullOrEmpty(token))
                                {
                                    context.Token = token;
                                }
                            }

                            return Task.CompletedTask;
                        },

                        // Expired/invalid token -> clean 401 the SPA can detect, so it can drive a top-level re-auth
                        // through the gateway (which silently refreshes against the still-valid SSO session). No HTML.
                        OnChallenge = context =>
                        {
                            context.Response.Headers[ping.ReAuthHeader] = "true";
                            return Task.CompletedTask;
                        },

                        OnTokenValidated = context =>
                        {
                            if (context.Principal?.Identity is not ClaimsIdentity identity)
                            {
                                return Task.CompletedTask;
                            }

                            if (identity.FindFirst(ping.NameClaimType)?.Value is { Length: > 0 } uid)
                            {
                                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, uid));
                            }

                            if (identity.FindFirst(ping.IsMemberOfClaim)?.Value is { Length: > 0 } memberOf)
                            {
                                foreach (
                                    string member in memberOf.Split(
                                        separator: '^',
                                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                                    )
                                )
                                {
                                    identity.AddClaim(new Claim(ClaimTypes.Role, member));
                                }
                            }

                            return Task.CompletedTask;
                        },
                    };
                }
            );
    }
}
