// Copyright (c) PNC Financial Services. All rights reserved.

using System.Collections.Frozen;
using System.Security.Claims;

namespace Dse.Sources;

/// <summary>
///     Resolves an authenticated user to the directory groups (principals) that govern what they can see. The
///     claims-only implementation reads the role claims projected from the Ping JWT; a future LDAP-backed
///     implementation (gated by <c>Dse:UseLdapConnectors</c>) enriches with transitive AD/OID membership.
/// </summary>
public interface IPrincipalResolver
{
    ValueTask<IReadOnlySet<Principal>> ResolveAsync(ClaimsPrincipal user, CancellationToken ct = default);
}

/// <summary>Principals come straight from the role claims on the token — no directory lookup.</summary>
public sealed class ClaimsPrincipalResolver : IPrincipalResolver
{
    public ValueTask<IReadOnlySet<Principal>> ResolveAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        IReadOnlySet<Principal> principals = user.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(Principal.From)
            .ToFrozenSet();

        return ValueTask.FromResult(principals);
    }
}
