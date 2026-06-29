// Copyright (c) PNC Financial Services. All rights reserved.

using System.Reflection;
using Microsoft.AspNetCore.OpenApi;

namespace Dse;

[AttributeUsage(AttributeTargets.Class)]
public sealed class OpenApiComponentAttribute : Attribute
{
    public string? Name { get; init; }
}

public static class OpenApiComponentAttributeExtensions
{
    extension(OpenApiOptions options)
    {
        public void AddComponentsFromAssemblies(params Assembly[] assemblies) =>
            options.AddDocumentTransformer(
                async (document, context, cancellationToken) =>
                {
                    foreach (
                        var type in assemblies
                            .SelectMany(a => a.GetTypes())
                            .Where(type =>
                                type is { IsAbstract: false, IsInterface: false }
                                && type.GetCustomAttribute<OpenApiComponentAttribute>() is not null
                            )
                    )
                    {
                        var attr = type.GetCustomAttribute<OpenApiComponentAttribute>()!;
                        var schema = await context.GetOrCreateSchemaAsync(type, parameterDescription: null, cancellationToken);
                        document.AddComponent(attr.Name ?? type.Name, schema);
                    }
                }
            );
    }
}
