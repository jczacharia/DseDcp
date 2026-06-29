// Copyright (c) PNC Financial Services. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace Dse;

[ExcludeFromCodeCoverage]
public static class CoreEnvironment
{
    public static readonly bool IsRelease =
        Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyConfigurationAttribute>()
            ?.Configuration.Equals("Release", StringComparison.OrdinalIgnoreCase)
        ?? false;

    public static readonly bool IsDebug = !IsRelease;

    public static readonly bool IsDocumentGenerationBuild =
        Assembly.GetEntryAssembly()?.GetName().Name?.Equals("GetDocument.Insider", StringComparison.OrdinalIgnoreCase)
            is true;

    public static readonly bool IsSpaProxyEnabled =
        Environment
            .GetEnvironmentVariable("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES")
            ?.Contains("Microsoft.AspNetCore.SpaProxy", StringComparison.OrdinalIgnoreCase)
        ?? false;

    public static readonly IReadOnlyDictionary<string, string?> AssemblyMetadata =
        Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(a => a.Key, a => a.Value, StringComparer.OrdinalIgnoreCase);

    public static readonly string RepoRoot =
        AssemblyMetadata["RepoRoot"] ?? throw new InvalidOperationException("RepoRoot metadata is missing.");

    public static readonly string NodePrefix =
        AssemblyMetadata["NodePrefix"] ?? throw new InvalidOperationException("NodePrefix metadata is missing.");

    public static bool ServesSpa => !IsDocumentGenerationBuild && !IsSpaProxyEnabled;

    extension(IHostEnvironment env)
    {
        public bool IsTest() => env.IsEnvironment("Test");

        public bool IsLocalBuild() => IsDebug && (env.IsDevelopment() || env.IsTest());

        public bool IsProductionBuild() => IsRelease && (env.IsProduction() || env.IsTest());
    }
}
