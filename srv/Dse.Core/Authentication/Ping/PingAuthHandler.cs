// Copyright (c) PNC Financial Services. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Dse.Authentication.Ping;

/// Authenticates by unwrapping the PingFederate access token from the gateway envelope and decoding it at the
/// userinfo endpoint — the endpoint validates the token, so this scheme needs no local signature verification.
public sealed class PingAuthHandler(
    IOptionsMonitor<PingAuthOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IPingAuthClient client,
    IMemoryCache cache
) : AuthenticationHandler<PingAuthOptions>(options, logger, encoder)
{
    private const string AccessTokenClaim = "access_token";
    private const string IsMemberOfClaim = "isMemberOf";
    private const string UidClaim = "uid";
    private const string ReAuthHeader = "X-Re-Auth-Required";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!TryGetEnvelope(out var envelope))
        {
            return AuthenticateResult.NoResult();
        }

        var accessToken = UnwrapAccessToken(envelope);

        // userinfo is the trust anchor, but reading the expiry locally lets us skip the round-trip for dead
        // tokens and cap the cache lifetime so a revoked group can't outlive the token.
        if (!TryGetExpiry(accessToken, out var expiresUtc))
        {
            return AuthenticateResult.Fail("Access token is malformed.");
        }

        if (expiresUtc <= DateTime.UtcNow)
        {
            return AuthenticateResult.Fail("Access token has expired.");
        }

        var cacheKey = $"ping-userinfo:{Fingerprint(accessToken)}";

        if (!cache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, string>? userInfo))
        {
            userInfo = await client.DecodeAccessTokenAsync(accessToken, Context.RequestAborted);

            if (userInfo is null)
            {
                return AuthenticateResult.Fail("User info could not be resolved.");
            }

            SetInCache(cacheKey, expiresUtc, userInfo);
        }

        ClaimsIdentity identity = new(Scheme.Name, UidClaim, ClaimTypes.Role);

        foreach (var (type, value) in userInfo!)
        {
            if (value is not { Length: > 0 })
            {
                continue;
            }

            if (type == IsMemberOfClaim)
            {
                foreach (var role in value.Split('^', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
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

    private void SetInCache(string cacheKey, DateTime expiresUtc, IReadOnlyDictionary<string, string> userInfo)
    {
        if (Options.CacheDuration > TimeSpan.Zero)
        {
            var ttl = expiresUtc - DateTime.UtcNow;
            cache.Set(cacheKey, userInfo, ttl < Options.CacheDuration ? ttl : Options.CacheDuration);
        }
    }

    // Signals the SPA to drive a top-level re-auth through the gateway rather than render the 401 itself.
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers[ReAuthHeader] = "true";
        return base.HandleChallengeAsync(properties);
    }

    private bool TryGetEnvelope([MaybeNullWhen(false)] out string envelope)
    {
        if (Context.Request.Cookies.TryGetValue(Options.CookieName, out var cookie) && !string.IsNullOrWhiteSpace(cookie))
        {
            envelope = cookie;
            return true;
        }

        if (
            Options.HeaderName is { Length: > 0 } headerName
            && Context.Request.Headers.TryGetValue(headerName, out var header)
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
            return new JsonWebToken(envelope).TryGetPayloadValue(AccessTokenClaim, out string? token) && token is { Length: > 0 } ? token : envelope;
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

    private static string Fingerprint(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
