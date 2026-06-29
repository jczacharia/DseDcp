// Copyright (c) PNC Financial Services. All rights reserved.

using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Dse.Authentication.Ping;

/// Validates the PingAccess gateway identity JWT (the PA.* cookie). The gateway authenticates the user against
/// the SSO session and forwards a signed JWT; this app independently verifies signature, issuer, audience and
/// expiry against the gateway's JWKS — it never trusts that the gateway already did so.
public sealed class PingJwtRegistration : IRegistration
{
    public static void Register(IHostApplicationBuilder builder)
    {
        builder
            .Services.AddHttpClient(PingJwtDefaults.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(static sp =>
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

        builder
            .Services.AddAuthentication(PingJwtDefaults.AuthenticationScheme)
            .AddJwtBearer(PingJwtDefaults.AuthenticationScheme);
        builder.Services.AddAuthorization();

        builder
            .Services.AddOptions<JwtBearerOptions>(PingJwtDefaults.AuthenticationScheme)
            .Configure<IServiceProvider>(
                static (jwt, sp) =>
                {
                    PingJwtOptions ping = sp.GetRequiredService<IOptions<PingJwtOptions>>().Value;
                    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();

                    jwt.RequireHttpsMetadata = ping.RequireHttpsMetadata;
                    jwt.MapInboundClaims = false;

                    // PingAccess publishes a raw JWKS, not an OIDC discovery document, so drive a ConfigurationManager
                    // over the JWKS directly. It caches keys and refreshes on rollover.
                    jwt.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                        ping.JwksUri,
                        sp.GetRequiredService<JwksConfigurationRetriever>(),
                        new HttpDocumentRetriever(httpClientFactory.CreateClient(PingJwtDefaults.HttpClientName))
                        {
                            RequireHttps = ping.RequireHttpsMetadata,
                        }
                    );

                    jwt.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = ping.Issuer,
                        ValidateAudience = true,
                        ValidAudience = ping.Audience,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        RequireSignedTokens = true,
                        RequireExpirationTime = true,
                        NameClaimType = ping.NameClaimType,
                        RoleClaimType = ClaimTypes.Role,
                        ClockSkew = TimeSpan.FromSeconds(30),
                    };

                    jwt.Events = new JwtBearerEvents
                    {
                        // The gateway delivers the JWT in the PA.* cookie; fall back to a configured header. When neither
                        // is present the handler's default Authorization: Bearer extraction runs (local/API-client use).
                        OnMessageReceived = context =>
                        {
                            if (
                                context.Request.Cookies.TryGetValue(ping.CookieName, out string? cookie)
                                && !string.IsNullOrEmpty(cookie)
                            )
                            {
                                context.Token = cookie;
                            }
                            else if (
                                !string.IsNullOrEmpty(ping.HeaderName)
                                && context.Request.Headers.TryGetValue(ping.HeaderName, out var header)
                                && header.FirstOrDefault() is { Length: > 0 } headerToken
                            )
                            {
                                context.Token = headerToken;
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

                        // isMemberOf is a single caret-delimited string; expand it into role claims for authorization.
                        OnTokenValidated = context =>
                        {
                            if (context.Principal?.Identity is not ClaimsIdentity identity)
                            {
                                return Task.CompletedTask;
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
