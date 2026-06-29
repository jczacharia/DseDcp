// Copyright (c) PNC Financial Services. All rights reserved.

using System.Security.Claims;
using Dse.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Dse.Authentication;

/// <summary>
///     Returns information about the currently authenticated user.
/// </summary>
/// <param name="Name">Current user's name.</param>
/// <param name="Claims">Current user's claims.</param>
public sealed record UserInfoResponse(string? Name, IReadOnlyList<ClaimDto> Claims);

/// <summary>
///     Represents an endpoint that returns information about the currently authenticated user.
/// </summary>
public sealed class UserInfoEndpoint : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder builder) =>
        builder
            .MapGet(
                "/userinfo",
                (ClaimsPrincipal user) =>
                    TypedResults.Ok(
                        new UserInfoResponse(
                            user.Identity?.Name,
                            [.. user.Claims.Select(c => new ClaimDto(c.Type, c.Value))]
                        )
                    )
            )
            .RequireAuthorization();
}
