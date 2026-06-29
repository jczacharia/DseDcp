// Copyright (c) PNC Financial Services. All rights reserved.

using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

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
                        Type type in assemblies
                            .SelectMany(a => a.GetTypes())
                            .Where(type =>
                                type is { IsAbstract: false, IsInterface: false }
                                && type.GetCustomAttribute<OpenApiComponentAttribute>() is not null
                            )
                    )
                    {
                        OpenApiComponentAttribute attr = type.GetCustomAttribute<OpenApiComponentAttribute>()!;
                        OpenApiSchema schema = await context.GetOrCreateSchemaAsync(
                            type,
                            parameterDescription: null,
                            cancellationToken
                        );
                        _ = document.AddComponent(attr.Name ?? type.Name, schema);
                    }
                }
            );
    }
}
