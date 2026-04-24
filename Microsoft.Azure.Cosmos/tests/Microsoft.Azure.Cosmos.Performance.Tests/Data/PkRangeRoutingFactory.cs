//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Data
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Loads the committed <c>shared_conversations_pkranges.tsv</c> dataset (17,329 real PKRanges
    /// from a production-shape collection) and exposes helpers for the end-to-end
    /// <c>DirectModeRoutingBenchmark</c>.
    /// </summary>
    /// <remarks>
    /// <para>TSV format: header row <c>PKRangeId\tMinEPK\tMaxEPK</c> + one data row per range.</para>
    /// <para>The exporter wrote the very first range's MinEPK as the single character <c>"0"</c>;
    /// the SDK expects <c>""</c> (<see cref="PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey"/>).
    /// <see cref="LoadFromTsv"/> normalizes this so <see cref="CollectionRoutingMap.TryCreateCompleteRoutingMap"/> accepts the map.</para>
    /// </remarks>
    internal static class PkRangeRoutingFactory
    {
        public const string EmptyMinSentinelInTsv = "0";
        public const string SdkMinSentinel = "";
        public const string SdkMaxSentinel = "FF";
        public const int ExpectedRowCount = 17329;

        /// <summary>
        /// Parses the TSV into <see cref="PartitionKeyRange"/> instances, normalizing the first
        /// range's <c>"0"</c> min sentinel to <c>""</c>. ResourceId is a single shared dummy value.
        /// </summary>
        public static IReadOnlyList<PartitionKeyRange> LoadFromTsv(string relativeOrAbsolutePath)
        {
            if (string.IsNullOrEmpty(relativeOrAbsolutePath))
            {
                throw new ArgumentException("Path is required.", nameof(relativeOrAbsolutePath));
            }

            string fullPath = Path.IsPathRooted(relativeOrAbsolutePath)
                ? relativeOrAbsolutePath
                : Path.Combine(AppContext.BaseDirectory, relativeOrAbsolutePath);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"PKRange TSV not found at '{fullPath}'.", fullPath);
            }

            string[] lines = File.ReadAllLines(fullPath);
            if (lines.Length < 2)
            {
                throw new InvalidDataException($"TSV at '{fullPath}' has no data rows.");
            }

            // Header check is loose: must start with "PKRangeId".
            if (!lines[0].StartsWith("PKRangeId", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Unexpected TSV header: '{lines[0]}'.");
            }

            List<PartitionKeyRange> ranges = new List<PartitionKeyRange>(lines.Length - 1);
            const string sharedResourceId = "ccZ1ANCszwkDAAAAAAAAUA==";

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] parts = line.Split('\t');
                if (parts.Length != 3)
                {
                    throw new InvalidDataException(
                        $"Malformed TSV row {i} (expected 3 tab-separated columns, got {parts.Length}): '{line}'.");
                }

                string min = parts[1];
                if (string.Equals(min, EmptyMinSentinelInTsv, StringComparison.Ordinal))
                {
                    min = SdkMinSentinel;
                }

                ranges.Add(new PartitionKeyRange
                {
                    Id = parts[0],
                    MinInclusive = min,
                    MaxExclusive = parts[2],
                    ResourceId = sharedResourceId,
                });
            }

            return ranges;
        }

        /// <summary>
        /// Produces the gateway-compatible <c>/pkranges</c> response body
        /// (<c>{ "_rid": ..., "_count": ..., "PartitionKeyRanges": [...] }</c>).
        /// Mirrors <c>PartitionKeyRangeFailoverTests/MockSetupsHelper.cs:242-247</c>.
        /// </summary>
        public static byte[] SerializePkRangeFeedJson(
            IReadOnlyList<PartitionKeyRange> ranges,
            string containerResourceId)
        {
            if (ranges == null)
            {
                throw new ArgumentNullException(nameof(ranges));
            }
            if (string.IsNullOrEmpty(containerResourceId))
            {
                throw new ArgumentException("Container RID is required.", nameof(containerResourceId));
            }

            JObject feed = new JObject
            {
                { "_rid", containerResourceId },
                { "_count", ranges.Count },
                { "PartitionKeyRanges", JArray.FromObject(ranges) },
            };

            return Encoding.UTF8.GetBytes(feed.ToString(Newtonsoft.Json.Formatting.None));
        }

        /// <summary>
        /// Deterministic pool of random partition-key strings for the benchmark body.
        /// Uses <see cref="Random"/> with the supplied seed so every run sees the same stream.
        /// </summary>
        public static string[] GenerateRandomPartitionKeyStrings(int count, int seed)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            Random rng = new Random(seed);
            string[] pool = new string[count];
            byte[] scratch = new byte[16];
            for (int i = 0; i < count; i++)
            {
                rng.NextBytes(scratch);
                pool[i] = Convert.ToBase64String(scratch);
            }
            return pool;
        }
    }
}
