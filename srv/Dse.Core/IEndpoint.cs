// Copyright (c) PNC Financial Services. All rights reserved.

using Microsoft.AspNetCore.Routing;

namespace Dse;

public interface IEndpoint
{
    public static abstract void MapEndpoint(IEndpointRouteBuilder builder);
}
