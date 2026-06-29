// Copyright (c) PNC Financial Services. All rights reserved.

using System.Security.Claims;
using Dse.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Dse.Authentication;

/// <summary>
/// Represents an endpoint that returns information about the currently authenticated user.
/// </summary>
public sealed class UserInfoEndpoint : IEndpoint
{
    /// <summary>
    /// Returns information about the currently authenticated user.
    /// </summary>
    /// <param name="Name">Current user's name.</param>
    /// <param name="Claims">Current user's claims.</param>
    [OpenApiComponent(Name = "UserInfoResponse")]
    public sealed record Response(string? Name, IReadOnlyList<ClaimDto> Claims);

    public static void MapEndpoint(IEndpointRouteBuilder builder) =>
        builder
            .MapGet(
                "/me",
                (ClaimsPrincipal user) =>
                    TypedResults.Ok(
                        new Response(user.Identity?.Name, user.Claims.Select(c => new ClaimDto(c.Type, c.Value)).ToList())
                    )
            )
            .RequireAuthorization();
}
