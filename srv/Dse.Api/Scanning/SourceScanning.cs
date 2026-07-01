// Copyright (c) PNC Financial Services. All rights reserved.

using System.Reflection;
using Dse.Sources;
using Elastic.Mapping;
using ServiceScan.SourceGenerator;

namespace Dse.Api.Scanning;

public static partial class SourceScanning
{
    [ScanForTypes(AssignableTo = typeof(ISourceModule), Handler = nameof(AddSource), AssemblyNameFilter = "Dse.*")]
    private static partial IServiceCollection AddSources(this IServiceCollection services);

    private static void AddSource<T>(IServiceCollection services)
        where T : class, ISourceModule, new()
    {
        var module = new T();

        if (module.MappingContextType.GetCustomAttribute<ElasticsearchMappingContextAttribute>() is null)
        {
            throw new InvalidOperationException(
                $"Source module {typeof(T).Name} mapping context type {module.MappingContextType.Name} is missing the required {nameof(ElasticsearchMappingContextAttribute)}."
            );
        }

        var regServicesMethod = module.MappingContextType.GetMethod(
            "RegisterServiceProvider",
            BindingFlags.Public | BindingFlags.Static
        );

        services.AddWebAppExtender(c =>
        {
            regServicesMethod?.Invoke(null, [c.Services]);
        });

        module.Build(new(module, services));
        services.AddSingleton(module);
    }

    [GenerateServiceRegistrations(
        AssignableTo = typeof(IStaticMappingResolver<>),
        AsImplementedInterfaces = true,
        AsSelf = true,
        Lifetime = ServiceLifetime.Singleton,
        AssemblyNameFilter = "Dse.*"
    )]
    private static partial IServiceCollection AddResolvers(this IServiceCollection services);

    extension(IServiceCollection services)
    {
        public IServiceCollection AddScannedSources() => services.AddSources().AddResolvers();
    }
}
