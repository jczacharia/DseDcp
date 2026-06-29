// Copyright (c) PNC Financial Services. All rights reserved.

namespace Dse.Models;

/// <summary>
/// Represents a claim associated with a user.
/// </summary>
/// <param name="Type">Claim type.</param>
/// <param name="Value">Claim value.</param>
public sealed record ClaimDto(string Type, string Value);
