// Copyright (c) PNC Financial Services. All rights reserved.

using System.Text.RegularExpressions;
using Vogen;

namespace Dse.Sources;

/// <summary>
///     Value object identifier for one index within a source — e.g. <c>incident</c>, <c>change</c>,
///     <c>business-app</c> under ServiceNow. A source owns one or more of these; Confluence's single index is the
///     degenerate case.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
public readonly partial record struct IndexKey
{
    [GeneratedRegex("^[a-z][a-z0-9-]{0,29}$")]
    private static partial Regex Pattern();

    private static Validation Validate(string input) =>
        Pattern().IsMatch(input)
            ? Validation.Ok
            : Validation.Invalid("Must be 1-30 chars, lowercase alphanumeric, and start with a letter.");

    private static string NormalizeInput(string input) => input.Trim().ToLowerInvariant();
}
