// Copyright (c) PNC Financial Services. All rights reserved.

using Vogen;

namespace Dse.Sources;

/// <summary>
///     An access principal — a directory group identity (LDAP DN in production, or the raw group token a
///     claims-only resolver yields). The single currency of authorization: both the values a user resolves to
///     and the values stamped into a document's <c>_allow_access_control</c> are principals. Matching is exact,
///     so index-time and search-time values must agree on format and case.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
public readonly partial record struct Principal
{
    private static Validation Validate(string input) =>
        string.IsNullOrWhiteSpace(input) ? Validation.Invalid("Must be non-empty.") : Validation.Ok;

    private static string NormalizeInput(string input) => input.Trim();
}
