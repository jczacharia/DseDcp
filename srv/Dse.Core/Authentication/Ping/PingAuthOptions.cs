// Copyright (c) PNC Financial Services. All rights reserved.

using FluentValidation;
using Microsoft.AspNetCore.Authentication;

namespace Dse.Authentication.Ping;

[Options(Name = PingAuthDefaults.AuthenticationScheme, Path = PingAuthDefaults.AuthenticationScheme)]
public class PingAuthOptions : AuthenticationSchemeOptions
{
    /// <summary>
    ///     PingAccess delivers the identity envelope in this cookie <c>(PA.{AppName}</c>).
    /// </summary>
    public string CookieName { get; set; } = "PA.APP_DSE";

    /// <summary>
    ///     Optional custom header the gateway may place the envelope in. Checked when the cookie is absent.
    /// </summary>
    public string? HeaderName { get; set; }

    /// <summary>
    ///     Base address of the PingFederate host exposing the userinfo decode endpoint.
    /// </summary>
    public string BaseAddress { get; set; } = "https://wfsso-apps.pnc.com";

    /// <summary>
    ///     Optional forward proxy for reaching Ping from inside the cluster.
    /// </summary>
    public string? ProxyAddress { get; set; }

    /// <summary>
    ///     Upper bound on how long a userinfo result is cached per token. Bounds group-membership revocation lag.
    ///     A time span of<c>0</c> turns off the cache.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);
}

public sealed class PingAuthOptionsValidator : AbstractValidator<PingAuthOptions>
{
    public PingAuthOptionsValidator()
    {
        _ = RuleFor(x => x.CookieName).NotEmpty();
        _ = RuleFor(x => x.BaseAddress).NotEmpty().Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute));
        _ = RuleFor(x => x.CacheDuration).GreaterThanOrEqualTo(TimeSpan.Zero);
        _ = RuleFor(x => x.ProxyAddress)
            .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute))
            .When(x => !string.IsNullOrWhiteSpace(x.ProxyAddress));
    }
}
