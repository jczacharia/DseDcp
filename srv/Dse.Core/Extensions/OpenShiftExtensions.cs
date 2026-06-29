// Copyright (c) PNC Financial Services. All rights reserved.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dse.Extensions;

public static class OpenShiftExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddOpenShiftIntegration() =>
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
        public void UseOpenShiftIntegration()
        {
            // must run before anything that reads scheme/host
            app.UseForwardedHeaders();

            if (app.Environment.IsProduction())
            {
                app.UseHsts();
            }

            // Authenticated requests must not be cached so that, after a Ping logout, the browser can't redisplay them from cache.
            app.Use((context, next) =>
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
                                || ctx.Response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase)
                                    is true
                            )
                            {
                                const string NoStore =
                                    "max-age=0, no-cache, no-store, must-revalidate, private, proxy-revalidate, no-transform";

                                ctx.Response.Headers.CacheControl = NoStore;
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
