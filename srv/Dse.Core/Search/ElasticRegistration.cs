// Copyright (c) PNC Financial Services. All rights reserved.

using Dse.Extensions;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using HealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

namespace Dse.Search;

public sealed class ElasticRegistration : IRegistration
{
    public static void Register(IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton(BuildTransport);

        builder.Services.AddSingleton<ElasticStartupService>();
        builder.Services.AddHostedService(static sp => sp.GetRequiredService<ElasticStartupService>());

        builder.Services.AddSingleton<ITransport>(static sp =>
            sp.GetRequiredService<DistributedTransport<IElasticsearchClientSettings>>()
        );

        builder.Services.AddSingleton(static sp => new ElasticsearchClient(
            sp.GetRequiredService<DistributedTransport<IElasticsearchClientSettings>>()
        ));

        builder
            .Services.AddHealthChecks()
            .AddCheck<ElasticHealthCheck>(
                "elastic",
                HealthStatus.Unhealthy,
                ["ready"],
                HealthCheckDefaults.ReadinessTimeout
            );
    }

    private static DistributedTransport<IElasticsearchClientSettings> BuildTransport(IServiceProvider sp)
    {
        var env = sp.GetRequiredService<IHostEnvironment>();
        var opts = sp.GetRequiredService<IOptions<ElasticOptions>>().Value;
        var es = new ElasticsearchClientSettings(new SingleNodePool(new(opts.BaseAddress)));

        if (!string.IsNullOrWhiteSpace(opts.ApiKey))
        {
            es = es.Authentication(new ApiKey(opts.ApiKey));
        }

        if (!string.IsNullOrWhiteSpace(opts.Username) && !string.IsNullOrWhiteSpace(opts.Password))
        {
            es = es.Authentication(new BasicAuthentication(opts.Username, opts.Password));
        }

        es = es
            // Multi-node pools cap retries at the remaining known nodes; single-node pools still have no failover target.
            .MaximumRetries(opts.MaximumRetries)
            .RequestTimeout(opts.RequestTimeout)
            .MaxRetryTimeout(opts.MaxRetryTimeout)
            .ConnectionLimit(opts.ConnectionLimit);

        // Multi-node distribution/failover tuning. No-ops on a single-node pool; opt-in elsewhere so the
        // transport defaults stand unless explicitly overridden.
        if (opts.DisablePinging)
        {
            es = es.DisablePing();
        }

        if (opts.PingTimeout is { } pingTimeout)
        {
            es = es.PingTimeout(pingTimeout);
        }

        if (opts.DeadTimeout is { } deadTimeout)
        {
            es = es.DeadTimeout(deadTimeout);
        }

        if (opts.MaxDeadTimeout is { } maxDeadTimeout)
        {
            es = es.MaxDeadTimeout(maxDeadTimeout);
        }

        // Trust an internally-issued HTTP-layer certificate by its fingerprint (prod-safe).
        if (!string.IsNullOrWhiteSpace(opts.CertificateFingerprint))
        {
            es = es.CertificateFingerprint(opts.CertificateFingerprint);
        }

        // Development-only: blanket-accept server certificates. Never honored outside Development.
        if (opts.AllowUntrustedCertificates && env.IsDevelopment())
        {
            es = es.ServerCertificateValidationCallback(static (_, _, _, _) => true);
        }

        if (opts.EnableHttpCompression)
        {
            // gzip the (large) bulk ingestion payloads
            es = es.EnableHttpCompression();
        }

        return new(env.IsDevelopment() ? es.EnableDebugMode() : es);
    }
}
