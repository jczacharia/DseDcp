// Copyright (c) PNC Financial Services. All rights reserved.

using FluentValidation;

namespace Dse.Authentication.Ping;

[Options(Path = PingJwtDefaults.OptionsPath)]
public sealed class PingJwtOptions
{
    /// PingAccess delivers the identity JWT in this cookie (PA.&lt;app&gt;).
    public string CookieName { get; set; } = "PA.APP_DSE";

    /// Optional custom header the gateway may place the JWT in (PNC "jwtHeaderName"). Checked when the cookie is absent.
    public string? HeaderName { get; set; }

    /// JWKS endpoint serving PingAccess's identity-token signing keys. Ping-provided, per environment.
    public string JwksUri { get; set; } = "https://wfsso-apps.pnc.com/ext/JwtSigning";

    /// Expected token issuer (the gateway, not the embedded access token's issuer).
    public string Issuer { get; set; } = "PingAccess";

    /// Expected audience — this application's registered id.
    public string Audience { get; set; } = "APP_DSE";

    /// Claim holding caret-delimited group memberships; each is projected to a role.
    public string IsMemberOfClaim { get; set; } = "isMemberOf";

    /// Claim used as the principal's name.
    public string NameClaimType { get; set; } = "uid";

    /// Response header set on a 401 so the SPA can drive a top-level re-auth through the gateway.
    public string ReAuthHeader { get; set; } = "X-Re-Auth-Required";

    /// Require HTTPS for JWKS retrieval. Keep true everywhere except local loopback testing.
    public bool RequireHttpsMetadata { get; set; } = true;

    /// Optional forward proxy for reaching the JWKS endpoint from inside the cluster.
    public string? ProxyAddress { get; set; }
}

public sealed class PingJwtOptionsValidator : AbstractValidator<PingJwtOptions>
{
    public PingJwtOptionsValidator()
    {
        RuleFor(x => x.CookieName).NotEmpty();
        RuleFor(x => x.JwksUri).NotEmpty().Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute));
        RuleFor(x => x.Issuer).NotEmpty();
        RuleFor(x => x.Audience).NotEmpty();
        RuleFor(x => x.IsMemberOfClaim).NotEmpty();
        RuleFor(x => x.NameClaimType).NotEmpty();
        RuleFor(x => x.ReAuthHeader).NotEmpty();
        RuleFor(x => x.ProxyAddress)
            .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute))
            .When(x => !string.IsNullOrWhiteSpace(x.ProxyAddress));
    }
}
