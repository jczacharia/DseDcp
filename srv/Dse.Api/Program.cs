// Copyright (c) PNC Financial Services. All rights reserved.

using Dse;
using Dse.Api.Scanning;
using Dse.Authentication.Ping;
using Dse.Confluence;
using Dse.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets("dse");

// Source Generated Registrations
builder.Services.AddDseOptions();
builder.Services.AddDseValidators();
builder.Services.AddOpenShiftIntegration();
builder.AddRegistrations();

builder.Services.AddProblemDetails(static s => s.ApplyCoreCustomization());
builder.Services.AddScoped<ProblemDetailsFactory, DefaultProblemDetailsFactory>();
builder.Services.ConfigureHttpClientDefaults(static o => o.RemoveAllLoggers());
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(PingJwtDefaults.AuthenticationScheme);
builder.Services.AddAuthorizationBuilder().SetDefaultPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Host.UseDefaultServiceProvider(static options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi(opts =>
{
    opts.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
    opts.MapVogenTypesInDse();
    opts.AddComponentsFromAssemblies([.. AppDomain.CurrentDomain.GetAssemblies(), typeof(ConfluenceDoc).Assembly]);
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

WebApplication app = builder.Build();

app.UseOpenShiftIntegration();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapDseHealthChecks();

app.MapOpenApi();
app.MapScalarApiReference();

app.UseAuthentication();
app.UseAuthorization();

RouteGroupBuilder api = app.MapGroup("api").WithTags("Api").RequireAuthorization();
api.MapApiEndpoints();

foreach (WebAppExtender reg in app.Services.GetServices<WebAppExtender>())
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
