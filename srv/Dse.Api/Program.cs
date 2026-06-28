// Copyright (c) PNC Financial Services. All rights reserved.

using System.Net;
using Dse;
using Dse.Api.Scanning;
using Dse.Extensions;
using Dse.ServiceDefaults;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddOpenshiftIntegration();

builder.Services.AddDseOptions();
builder.Services.AddDseValidators();

builder.Services.AddProblemDetails(static s => s.ApplyCoreCustomization());
builder.Services.AddScoped<ProblemDetailsFactory, DefaultProblemDetailsFactory>();
builder.Services.ConfigureHttpClientDefaults(static o => o.RemoveAllLoggers());
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi(opts =>
{
    opts.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
    opts.MapVogenTypesInDse();
    opts.AddComponentsFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());
    opts.AddDocumentTransformer(
        static (doc, _, _) =>
        {
            doc.Info.Title = "DSE";
            doc.Info.Description = "Enterprise Search";
            return Task.CompletedTask;
        }
    );
});

if (CoreEnvironment.IsDocumentGenerationBuild)
{
    // Don't validate when generating OpenAPI documents; else with throw
    builder.Services.RemoveAll<IStartupValidator>();
}

WebApplication app = builder.Build();

app.UseOpenshiftIntegration();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.MapScalarApiReference();

app.UseDefaultFiles();
app.UseStaticFiles();

// app.UseAuthentication();
// app.UseAuthorization();

RouteGroupBuilder api = app.MapGroup("api").WithTags("Api").RequireAuthorization();
api.MapApiEndpoints();

foreach (WebAppExtender reg in app.Services.GetServices<WebAppExtender>())
{
    reg.Register(app);
}

app.MapFallback((HttpContext context) => context.ProblemHttpResult(HttpStatusCode.NotFound, "Endpoint not found."));
app.MapFallbackToFile("index.html").AllowAnonymous();

await app.RunAsync();
