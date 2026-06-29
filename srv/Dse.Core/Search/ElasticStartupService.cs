// Copyright (c) PNC Financial Services. All rights reserved.

using System.Globalization;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ByteSize = Humanizer.ByteSize;

namespace Dse.Search;

public sealed class ElasticStartupService(
    ILogger<ElasticStartupService> logger,
    ElasticsearchClient client,
    IOptionsMonitor<ElasticOptions> options
) : BackgroundService
{
    private const long DefaultBulkMaxByteSize = 100L * 1024 * 1024;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Elasticsearch starting...");

        try
        {
            await ProbeClusterAsync(stoppingToken);
            logger.LogInformation("Elasticsearch started successfully");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Elasticsearch startup probe failed");
        }
    }

    private async Task ProbeClusterAsync(CancellationToken ct)
    {
        NodesInfoResponse response = await client.Nodes.InfoAsync(null, Metrics.All, ct);
        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException($"Nodes info failed: {response.DebugInformation}");
        }

        int dataNodeCount = 0;
        int writePoolCapacity = 0;
        long bulkMaxByteSize = long.MaxValue;

        foreach ((string _, NodeInfo node) in response.Nodes)
        {
            bool isDataNode = node.Roles.Any(r => r.ToString().StartsWith("data", StringComparison.OrdinalIgnoreCase));

            if (!isDataNode)
            {
                continue;
            }

            dataNodeCount++;

            if (
                node.ThreadPool is { } pools
                && pools.TryGetValue("write", out NodeThreadPoolInfo? write)
                && write.Size is { } size
            )
            {
                writePoolCapacity += size;
            }

            if (node.Http?.MaxContentLengthInBytes is { } maxBytes && maxBytes < bulkMaxByteSize)
            {
                bulkMaxByteSize = maxBytes;
            }
        }

        if (writePoolCapacity <= 0)
        {
            throw new InvalidOperationException(
                $"No data nodes reported a 'write' thread pool size: {response.DebugInformation}"
            );
        }

        if (bulkMaxByteSize == long.MaxValue)
        {
            bulkMaxByteSize = DefaultBulkMaxByteSize;
        }

        // The write pool caps what the cluster can absorb; MaxExportConcurrency caps what a single (possibly
        // single-core) client can actually drive. The smaller wins. Core count is deliberately not a factor —
        // this export path is I/O-bound, not CPU-bound.
        int clusterCeiling = Math.Max(2, (int)(writePoolCapacity * options.CurrentValue.NodeUtilization));
        int maxChannelConcurrency = Math.Min(clusterCeiling, Math.Max(2, options.CurrentValue.MaxExportConcurrency));

        logger.LogInformation(
            "Cluster sizing: {@ClusterSizing}",
            new
            {
                bulkMaxByteSize = ByteSize.FromBytes(bulkMaxByteSize).ToString(CultureInfo.InvariantCulture),
                clusterCeiling,
                dataNodeCount,
                maxChannelConcurrency,
                options.CurrentValue.MaxExportConcurrency,
                options.CurrentValue.NodeUtilization,
                writePoolCapacity,
            }
        );

        options.CurrentValue.MaxChannelConcurrency = maxChannelConcurrency;
        options.CurrentValue.BulkMaxByteSize = bulkMaxByteSize;
        options.CurrentValue.DataNodeCount = dataNodeCount;
    }
}
