// Copyright (c) PNC Financial Services. All rights reserved.

using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using ServiceScan.SourceGenerator;

namespace Dse.Api.Scanning;

public static partial class OpenApiComponentScanning
{
    [ScanForTypes(AttributeFilter = typeof(OpenApiComponentAttribute), AssemblyNameFilter = "Dse.*")]
    private static partial IEnumerable<Type> GetAllApiComponentTypes();

    extension(OpenApiOptions options)
    {
        public void AddDseApiComponents() =>
            options.AddDocumentTransformer(
                async (document, context, cancellationToken) =>
                {
                    foreach (var type in GetAllApiComponentTypes())
                    {
                        var attr = type.GetCustomAttribute<OpenApiComponentAttribute>()!;
                        var schema = await context.GetOrCreateSchemaAsync(type, null, cancellationToken);
                        document.AddComponent(attr.Name ?? type.Name, schema);
                    }
                }
            );
    }
}
