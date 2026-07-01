// Copyright (c) PNC Financial Services. All rights reserved.

using Dse;
using Dse.Api.Scanning;
using Dse.Authentication.Ping;
using Dse.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets("dse");
builder.Services.AddOpenShiftIntegration();

builder
    .Services.AddScannedOptions()
    .AddScannedValidators()
    .AddScannedSources()
    .AddScannedServices()
    .AddScannedElasticDocConfig()
    .AddProblemDetails(static s => s.ApplyCoreCustomization())
    .AddScoped<ProblemDetailsFactory, DefaultProblemDetailsFactory>()
    .ConfigureHttpClientDefaults(static o => o.RemoveAllLoggers())
    .AddMemoryCache()
    .AddHttpContextAccessor();

builder.Services.AddAuthentication(PingAuthDefaults.AuthenticationScheme);
builder.Services.AddAuthorizationBuilder().SetDefaultPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Host.UseDefaultServiceProvider(static options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder
    .Services.AddEndpointsApiExplorer()
    .AddOpenApi(opts =>
    {
        opts.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
        opts.MapVogenTypesInDse();
        opts.AddDseApiComponents();
        opts.AddDocumentTransformer(
            static (doc, _, _) =>
            {
                doc.Info.Title = "DSE";
                doc.Info.Description = "Enterprise Search";
                return Task.CompletedTask;
            }
        );
    });

builder.Services.RemoveWindowsEventLogProvider();

if (CoreEnvironment.IsDocumentGenerationBuild)
{
    // Remove all startup services when document generation build.
    builder.Services.RemoveAll<IStartupValidator>();
    builder.Services.RemoveAll<IHostedService>();
}

var app = builder.Build();

app.UseOpenShiftIntegration();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapDseHealthChecks();

app.MapOpenApi();
app.MapScalarApiReference();

app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("api").WithTags("Api").RequireAuthorization();
api.MapScannedEndpoints();

foreach (var reg in app.Services.GetServices<WebAppExtender>())
{
    reg.Register(app);
}

if (CoreEnvironment.ServesSpa)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html").AllowAnonymous();
}

await app.RunAsync();
