// Copyright (c) PNC Financial Services. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Dse.Authentication.Ping;

/// Authenticates by unwrapping the PingFederate access token from the gateway envelope and decoding it at the
/// userinfo endpoint — the endpoint validates the token, so this scheme needs no local signature verification.
public sealed class PingAuthHandler(
    IOptionsMonitor<PingAuthOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache
) : AuthenticationHandler<PingAuthOptions>(options, logger, encoder)
{
    private const string AccessTokenClaim = "access_token";
    private const string IsMemberOfClaim = "isMemberOf";
    private const string UidClaim = "uid";
    private const string ReAuthHeader = "X-Re-Auth-Required";
    private const string UserInfoPath = "/idp/userinfo.openid?schema=openid&access_token=";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!TryGetEnvelope(out string? envelope))
        {
            return AuthenticateResult.NoResult();
        }

        string accessToken = UnwrapAccessToken(envelope);

        // userinfo is the trust anchor, but reading the expiry locally lets us skip the round-trip for dead
        // tokens and cap the cache lifetime so a revoked group can't outlive the token.
        if (!TryGetExpiry(accessToken, out DateTime expiresUtc))
        {
            return AuthenticateResult.Fail("Access token is malformed.");
        }

        if (expiresUtc <= DateTime.UtcNow)
        {
            return AuthenticateResult.Fail("Access token has expired.");
        }

        string cacheKey = $"ping-userinfo:{Fingerprint(accessToken)}";
        if (!cache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, string?>? userInfo))
        {
            userInfo = await FetchUserInfoAsync(accessToken);
            if (userInfo is null)
            {
                return AuthenticateResult.Fail("User info could not be resolved.");
            }

            SetInCache(cacheKey, expiresUtc, userInfo);
        }

        ClaimsIdentity identity = new(Scheme.Name, UidClaim, ClaimTypes.Role);
        foreach ((string type, string? value) in userInfo!)
        {
            if (value is not { Length: > 0 })
            {
                continue;
            }

            if (type == IsMemberOfClaim)
            {
                foreach (
                    string role in value.Split(
                        separator: '^',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                    )
                )
                {
                    identity.AddClaim(new(ClaimTypes.Role, role));
                }
            }
            else
            {
                identity.AddClaim(new(type, value));
            }
        }

        return AuthenticateResult.Success(new(new(identity), Scheme.Name));
    }

    private void SetInCache(string cacheKey, DateTime expiresUtc, IReadOnlyDictionary<string, string?> userInfo)
    {
        if (Options.CacheDuration > TimeSpan.Zero)
        {
            TimeSpan ttl = expiresUtc - DateTime.UtcNow;
            _ = cache.Set(cacheKey, userInfo, ttl < Options.CacheDuration ? ttl : Options.CacheDuration);
        }
    }

    // Signals the SPA to drive a top-level re-auth through the gateway rather than render the 401 itself.
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers[ReAuthHeader] = "true";
        return base.HandleChallengeAsync(properties);
    }

    private async Task<IReadOnlyDictionary<string, string?>?> FetchUserInfoAsync(string accessToken)
    {
        HttpClient client = httpClientFactory.CreateClient(PingAuthDefaults.HttpClientName);
        using HttpResponseMessage response = await client.GetAsync(
            $"{UserInfoPath}{Uri.EscapeDataString(accessToken)}",
            Context.RequestAborted
        );

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<Dictionary<string, string?>>(Context.RequestAborted)
            : null;
    }

    private bool TryGetEnvelope([MaybeNullWhen(false)] out string envelope)
    {
        if (
            Context.Request.Cookies.TryGetValue(Options.CookieName, out string? cookie)
            && !string.IsNullOrWhiteSpace(cookie)
        )
        {
            envelope = cookie;
            return true;
        }

        if (
            Options.HeaderName is { Length: > 0 } headerName
            && Context.Request.Headers.TryGetValue(headerName, out StringValues header)
            && header.FirstOrDefault() is { Length: > 0 } headerValue
        )
        {
            envelope = headerValue;
            return true;
        }

        envelope = null;
        return false;
    }

    // The gateway envelope nests the access token in a claim; if the value isn't an envelope, treat it as the token.
    private static string UnwrapAccessToken(string envelope)
    {
        try
        {
            return
                new JsonWebToken(envelope).TryGetPayloadValue(AccessTokenClaim, out string? token)
                && token is { Length: > 0 }
                ? token
                : envelope;
        }
        catch (ArgumentException)
        {
            return envelope;
        }
    }

    private static bool TryGetExpiry(string token, out DateTime expiresUtc)
    {
        try
        {
            expiresUtc = new JsonWebToken(token).ValidTo;
            return expiresUtc != default;
        }
        catch (ArgumentException)
        {
            expiresUtc = default;
            return false;
        }
    }

    private static string Fingerprint(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
