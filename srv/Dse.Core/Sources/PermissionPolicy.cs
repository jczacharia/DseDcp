// Copyright (c) PNC Financial Services. All rights reserved.

using System.Collections.Frozen;

namespace Dse.Sources;

public enum GateMode
{
    /// <summary>The user must hold at least one gate role.</summary>
    Any,

    /// <summary>The user must hold every gate role.</summary>
    All,
}

/// <summary>
///     How an index is secured, composed of two independent levels that combine with AND:
///     <list type="bullet">
///         <item><b>Gate</b> — must the user hold the required group(s) to touch the index at all. Enforced as the
///         API key's index <em>read</em> privilege: no gate, no descriptor, no results.</item>
///         <item><b>Document level (DLS)</b> — per-document filtering on <c>_allow_access_control</c>, public
///         documents (no ACL field) remaining visible to anyone past the gate.</item>
///     </list>
///     Confluence today is <see cref="Gated" /> with DLS off (public-within-gate); turning DLS on later is a flip
///     to <see cref="Dls" />, additive and without touching the gate.
/// </summary>
public sealed record PermissionPolicy
{
    public const string AllowAccessControlField = "_allow_access_control";

    private readonly Func<IReadOnlySet<Principal>, IReadOnlyCollection<Principal>> _resolvePrincipals;

    private PermissionPolicy(
        IReadOnlySet<Principal> gateRoles,
        GateMode gateMode,
        bool documentLevel,
        Func<IReadOnlySet<Principal>, IReadOnlyCollection<Principal>>? resolvePrincipals
    )
    {
        GateRoles = gateRoles;
        GateMode = gateMode;
        DocumentLevel = documentLevel;
        _resolvePrincipals = resolvePrincipals ?? (static p => [.. p]);
    }

    public IReadOnlySet<Principal> GateRoles { get; }
    public GateMode GateMode { get; }
    public bool DocumentLevel { get; }

    /// <summary>No gate, no per-document filtering — visible to every authenticated user.</summary>
    public static PermissionPolicy Public { get; } = new(FrozenSet<Principal>.Empty, GateMode.Any, false, null);

    /// <summary>Public-within-gate: holding the gate group(s) grants access to every document. Confluence today.</summary>
    public static PermissionPolicy Gated(params Principal[] gateRoles) => new(gateRoles.ToFrozenSet(), GateMode.Any, false, null);

    /// <summary>
    ///     Per-document security: an optional gate plus a <c>_allow_access_control</c> filter. <paramref name="resolvePrincipals" />
    ///     narrows the user's full principal set to those relevant to this index (defaults to all of them).
    /// </summary>
    public static PermissionPolicy Dls(
        IEnumerable<Principal> gateRoles,
        Func<IReadOnlySet<Principal>, IReadOnlyCollection<Principal>>? resolvePrincipals = null
    ) => new(gateRoles.ToFrozenSet(), GateMode.Any, true, resolvePrincipals);

    /// <summary>Whether the user clears the gate and may therefore be granted a read descriptor for this index.</summary>
    public bool GatePasses(IReadOnlySet<Principal> principals) =>
        GateRoles.Count == 0 || (GateMode == GateMode.Any ? GateRoles.Overlaps(principals) : principals.IsSupersetOf(GateRoles));

    /// <summary>
    ///     The DLS query to embed in this index's role descriptor, or <c>null</c> when the gate alone governs (no
    ///     per-document filtering). Minted per-user with concrete principals, so no query template is needed.
    /// </summary>
    public object? BuildDlsQuery(IReadOnlySet<Principal> principals)
    {
        if (!DocumentLevel)
        {
            return null;
        }

        var access = _resolvePrincipals(principals).Select(static p => p.Value).ToArray();
        return new
        {
            @bool = new
            {
                should = new object[]
                {
                    new { @bool = new { must_not = new { exists = new { field = AllowAccessControlField } } } },
                    new { terms = new Dictionary<string, object> { [AllowAccessControlField] = access } },
                },
                minimum_should_match = 1,
            },
        };
    }
}
