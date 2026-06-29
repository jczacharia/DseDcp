// Copyright (c) PNC Financial Services. All rights reserved.

using FluentValidation;

namespace Dse.Authentication.Ping;

[Options(Path = PingJwtDefaults.OptionsPath)]
public sealed class PingJwtOptions
{
    public string CookieName { get; set; } = "PA.APP_DSE";
    public string JwksUri { get; set; } = "https://wfsso-apps.pnc.com/ext/JwtSigning";
    public string IsMemberOfClaim { get; set; } = "isMemberOf";
    public string NameClaimType { get; set; } = "uid";
    public string AccessTokenClaim { get; set; } = "access_token";
    public string ReAuthHeader { get; set; } = "X-Re-Auth-Required";
    public string? ProxyAddress { get; set; }
}

public sealed class PingJwtOptionsValidator : AbstractValidator<PingJwtOptions>
{
    public PingJwtOptionsValidator()
    {
        RuleFor(x => x.CookieName).NotEmpty();
        RuleFor(x => x.JwksUri).NotEmpty().Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute));
        RuleFor(x => x.IsMemberOfClaim).NotEmpty();
        RuleFor(x => x.NameClaimType).NotEmpty();
        RuleFor(x => x.AccessTokenClaim).NotEmpty();
        RuleFor(x => x.ReAuthHeader).NotEmpty();
        RuleFor(x => x.ProxyAddress)
            .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute))
            .When(x => !string.IsNullOrWhiteSpace(x.ProxyAddress));
    }
}
