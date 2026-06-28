// Copyright (c) PNC Financial Services. All rights reserved.

using Dse.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Dse.ServiceDefaults;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/aspire/service-defaults
public static class ServiceExtensions
{
    extension<TBuilder>(TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        public TBuilder AddServiceDefaults()
        {
            builder.Configuration.AddUserSecrets("dse");
            builder.ConfigureOpenTelemetry();
            builder.AddDefaultHealthChecks();
            builder.RemoveWindowsEventLogProvider();
            builder.Services.AddServiceDiscovery();
            builder.Services.ConfigureHttpClientDefaults(http => http.AddServiceDiscovery());

            if (builder is WebApplicationBuilder host)
            {
                host.Host.UseDefaultServiceProvider(static options =>
                {
                    options.ValidateScopes = true;
                    options.ValidateOnBuild = true;
                });
            }

            return builder;
        }

        public TBuilder ConfigureOpenTelemetry()
        {
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            });

            builder
                .Services.AddOpenTelemetry()
                .WithMetrics(metrics =>
                {
                    metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation();
                })
                .WithTracing(tracing =>
                {
                    tracing
                        .AddSource(builder.Environment.ApplicationName)
                        .AddAspNetCoreInstrumentation(t =>
                            t.Filter = context =>
                                !context.Request.Path.StartsWithSegments(HealthCheckEndpoints.HealthEndpointPath)
                        )
                        .AddHttpClientInstrumentation();
                });

            builder.AddOpenTelemetryExporters();

            return builder;
        }

        private void RemoveWindowsEventLogProvider()
        {
            const string eventLogProvider = "Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider";

            foreach (
                ServiceDescriptor descriptor in builder
                    .Services.Where(d =>
                        d.ServiceType == typeof(ILoggerProvider) && d.ImplementationType?.FullName == eventLogProvider
                    )
                    .ToList()
            )
            {
                builder.Services.Remove(descriptor);
            }
        }

        private TBuilder AddOpenTelemetryExporters()
        {
            bool useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

            if (useOtlpExporter)
            {
                builder.Services.AddOpenTelemetry().UseOtlpExporter();
            }

            return builder;
        }

        public TBuilder AddDefaultHealthChecks()
        {
            builder
                .Services.AddHealthChecks()
                // Add a default liveness check to ensure app is responsive
                .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

            return builder;
        }
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection AddOpenshiftIntegration() =>
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
                options.ForwardLimit = null; // gateway -> router is more than one hop
                options.KnownIPNetworks.Clear(); // only in-cluster infrastructure can reach the pod
                options.KnownProxies.Clear();
            });
    }

    extension(WebApplication app)
    {
        public void UseOpenshiftIntegration()
        {
            // must run before anything that reads scheme/host
            app.UseForwardedHeaders();

            if (app.Environment.IsProduction())
            {
                app.UseHsts();
            }

            // Authenticated requests must not be cached so that, after a Ping logout, the browser can't redisplay them from cache.
            app.Use(
                (context, next) =>
                {
                    context.Response.OnStarting(
                        static state =>
                        {
                            if (state is not HttpContext ctx)
                            {
                                return Task.CompletedTask;
                            }

                            if (
                                ctx.Request.Path.StartsWithSegments("/api")
                                || ctx.Response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) is true
                            )
                            {
                                const string noStore =
                                    "max-age=0, no-cache, no-store, must-revalidate, private, proxy-revalidate, no-transform";
                                ctx.Response.Headers.CacheControl = noStore;
                            }

                            return Task.CompletedTask;
                        },
                        context
                    );

                    return next(context);
                }
            );
        }
    }
}
