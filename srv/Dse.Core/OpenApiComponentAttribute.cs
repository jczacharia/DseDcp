// Copyright (c) PNC Financial Services. All rights reserved.

namespace Dse;

[AttributeUsage(AttributeTargets.Class)]
public sealed class OpenApiComponentAttribute : Attribute
{
    public string? Name { get; init; }
}
