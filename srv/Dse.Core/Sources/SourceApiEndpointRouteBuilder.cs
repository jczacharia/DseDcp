// Copyright (c) PNC Financial Services. All rights reserved.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Dse.Sources;

public static class SourceApiEndpointRouteBuilderExtensions
{
    private sealed class SourceEndpointsConventionBuilder(RouteGroupBuilder inner) : IEndpointConventionBuilder
    {
        private IEndpointConventionBuilder InnerAsConventionBuilder => inner;

        public void Add(Action<EndpointBuilder> convention) => InnerAsConventionBuilder.Add(convention);

        public void Finally(Action<EndpointBuilder> finallyConvention) => InnerAsConventionBuilder.Finally(finallyConvention);
    }
}
