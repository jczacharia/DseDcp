// Copyright (c) PNC Financial Services. All rights reserved.

// using Elastic.Mapping;
// using Microsoft.Extensions.DependencyInjection;
//
// namespace Dse.Sources;
//
// public abstract class SourceOptions
// {
//     /// <summary>Expected serialized size of a single document, in bytes. Drives the per-batch item count so a
//     /// batch's bytes land near the target; set it to the source's realistic average.</summary>
//     public long EstimatedDocBytes { get; set; } = 16 * 1024;
//
//     /// <summary>Fraction of the cluster's max bulk content length a single batch targets. Headroom below
//     /// <c>1.0</c> keeps every request safely under the hard <c>max_content_length</c> limit.</summary>
//     public double BatchByteFraction { get; set; } = 0.25d;
//
//     /// <summary>Lower clamp on the derived per-batch item count.</summary>
//     public int MinBatchItems { get; set; } = 1;
//
//     /// <summary>Upper clamp on the derived per-batch item count.</summary>
//     public int MaxBatchItems { get; set; } = 10_000;
//
//     /// <summary>Resolves the concrete per-batch dimensions for this profile against the cluster's max bulk content
//     /// length: a byte budget a single request targets, and the item count whose nominal bytes land near it.</summary>
//     /// <param name="clusterMaxContentBytes">The cluster's <c>http.max_content_length</c>, probed at startup.</param>
//     /// <returns>The per-batch item count and byte budget to apply to the channel buffer.</returns>
//     public (int Items, long Bytes) ResolveBatch(long clusterMaxContentBytes)
//     {
//         var bytes = Math.Clamp((long)(clusterMaxContentBytes * BatchByteFraction), 1L, clusterMaxContentBytes);
//         var items = bytes / Math.Max(1L, EstimatedDocBytes);
//         return ((int)Math.Clamp(items, MinBatchItems, Math.Max(MinBatchItems, MaxBatchItems)), bytes);
//     }
// }
//
// public sealed class SourceBuilder(SourceKey sourceKey, IServiceCollection services)
// {
//     public IServiceCollection Services { get; } = services;
//     public SourceKey SourceKey { get; } = sourceKey;
//
//     public SourceBuilder AddIndex(ElasticsearchTypeContext typeContext) => Services.AddKeyedSingleton(sourceKey, typeContext);
// }
