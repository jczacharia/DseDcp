// Copyright (c) PNC Financial Services. All rights reserved.

using Elastic.Transport;
using FluentValidation;

namespace Dse.Search;

[Options(Path = "Elastic")]
public sealed class ElasticOptions
{
    /// <summary>
    ///     Cluster endpoint URL. As the only endpoint it yields a <see cref="SingleNodePool" /> — no client-side
    ///     failover, so distribution is whatever sits behind it (e.g. a load-balancer VIP).
    /// </summary>
    public string BaseAddress { get; set; } = string.Empty;

    /// <summary>API-key authentication; takes precedence over <see cref="Username" />/<see cref="Password" /> when set.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Basic-auth username, used only when no <see cref="ApiKey" /> is provided.</summary>
    public string? Username { get; set; }

    /// <summary>Basic-auth password paired with <see cref="Username" />.</summary>
    public string? Password { get; set; }

    /// <summary>
    ///     Hex-encoded SHA-256 fingerprint of the cluster's HTTP-layer CA certificate. The prod-safe way to
    ///     trust an internally-issued certificate without installing the CA into the host trust store. Leave empty to
    ///     rely on the platform certificate chain.
    /// </summary>
    public string CertificateFingerprint { get; set; } = string.Empty;

    /// <summary>
    ///     Accept any server certificate. A development-only escape hatch — ignored outside Development. Use
    ///     <see cref="CertificateFingerprint" /> everywhere else.
    /// </summary>
    public bool AllowUntrustedCertificates { get; set; }

    /// <summary>Fraction of the cluster write thread-pool capacity this client may drive concurrently.</summary>
    public double NodeUtilization { get; set; } = 0.75d;

    /// <summary>
    ///     Absolute cap on concurrent bulk exports. For an I/O-bound ingest the binding limit is the client
    ///     itself, not core count; clamped down to the cluster write-pool ceiling at startup.
    /// </summary>
    public int MaxExportConcurrency { get; set; } = 30;

    /// <summary>
    ///     Gzip bulk payloads — trades CPU for network. Disable when Elasticsearch is network-local and the
    ///     client is CPU-bound (e.g. a single-core pod) so those cycles go to crawl/serialize instead.
    /// </summary>
    public bool EnableHttpCompression { get; set; } = true;

    /// <summary>Per-request timeout. A large bulk to a busy cluster can exceed the default under load.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    ///     Overall retry budget across attempts for a single call (a static node otherwise caps this at the request
    ///     timeout).
    /// </summary>
    public TimeSpan MaxRetryTimeout { get; set; } = TimeSpan.FromSeconds(180);

    /// <summary>
    ///     Transport-level retries for a transient or occasionally-slow node. The default covers all five production
    ///     nodes.
    /// </summary>
    public int MaximumRetries { get; set; } = 4;

    /// <summary>
    ///     Maximum concurrent HTTP connections per node endpoint. With a multi-node pool this is per node, so
    ///     total sockets scale with the node count.
    /// </summary>
    public int ConnectionLimit { get; set; } = 80;

    /// <summary>
    ///     Skip the pre-request liveness ping the transport otherwise issues to a revived or first-seen node in a
    ///     multi-node pool. Pinging catches a dead node before a (large) bulk is sent at it; disabling shaves a round-trip
    ///     at the cost of discovering the failure via the bulk itself. No effect on a single-node pool, which never pings.
    /// </summary>
    public bool DisablePinging { get; set; }

    /// <summary>Timeout for the liveness ping. Null leaves the transport default.</summary>
    public TimeSpan? PingTimeout { get; set; }

    /// <summary>
    ///     How long a node stays benched after being marked dead before it is tried again; the backoff grows on
    ///     repeated failures up to <see cref="MaxDeadTimeout" />. Null leaves the transport default (60s). Lower it to
    ///     bring a briefly-busy node back sooner; raise it to stop flapping a genuinely sick node.
    /// </summary>
    public TimeSpan? DeadTimeout { get; set; }

    /// <summary>Upper bound on the dead-node backoff. Null leaves the transport default.</summary>
    public TimeSpan? MaxDeadTimeout { get; set; }

    /// <summary>
    ///     How long to wait for the channel to flush all buffered docs to Elasticsearch before failing the run.
    ///     A full-corpus crawl on a large source can legitimately exceed the default.
    /// </summary>
    public TimeSpan BulkDrainTimeout { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    ///     The write pool caps what the cluster can absorb, post configured in <see cref="ElasticStartupService" />
    /// </summary>
    public int MaxChannelConcurrency { get; set; } = 15;

    /// <summary>
    ///     The maximum bulk size in bytes that the cluster can accept, post configured in <see cref="ElasticStartupService" />
    /// </summary>
    public long BulkMaxByteSize { get; set; }

    /// <summary>
    ///     The number of data nodes in the cluster, post configured in <see cref="ElasticStartupService" />
    /// </summary>
    public int DataNodeCount { get; set; }
}

public sealed class ElasticOptionsValidator : AbstractValidator<ElasticOptions>
{
    public ElasticOptionsValidator() =>
        RuleFor(x => x.BaseAddress).Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute));
}
