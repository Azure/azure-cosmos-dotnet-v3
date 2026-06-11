//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using Newtonsoft.Json;

    /// <summary>
    /// A single per-window, per-operation result row matching the dedicated .NET perf dashboard
    /// schema (Azure Data Explorer database <c>DotNetPerf</c>, table <c>PerfResults</c>). Property
    /// names are serialized to the exact column names the Grafana dashboard queries.
    /// </summary>
    internal sealed class PerfResultsRecord
    {
        [JsonProperty("TIMESTAMP")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("operation")]
        public string Operation { get; set; }

        [JsonProperty("p50_ms")]
        public double P50Ms { get; set; }

        [JsonProperty("p90_ms")]
        public double P90Ms { get; set; }

        [JsonProperty("p99_ms")]
        public double P99Ms { get; set; }

        [JsonProperty("mean_ms")]
        public double MeanMs { get; set; }

        [JsonProperty("count")]
        public long Count { get; set; }

        [JsonProperty("errors")]
        public long Errors { get; set; }

        [JsonProperty("error_message")]
        public string ErrorMessage { get; set; }

        [JsonProperty("error_status_code")]
        public int ErrorStatusCode { get; set; }

        [JsonProperty("hostname")]
        public string Hostname { get; set; }

        [JsonProperty("sdk_version")]
        public string SdkVersion { get; set; }

        [JsonProperty("commit_sha")]
        public string CommitSha { get; set; }

        [JsonProperty("config_concurrency")]
        public int ConfigConcurrency { get; set; }

        [JsonProperty("config_application_region")]
        public string ConfigApplicationRegion { get; set; }

        [JsonProperty("ru_per_sec")]
        public double RuPerSec { get; set; }

        [JsonProperty("gc_gen0_count")]
        public long GcGen0Count { get; set; }

        [JsonProperty("gc_gen1_count")]
        public long GcGen1Count { get; set; }

        [JsonProperty("gc_gen2_count")]
        public long GcGen2Count { get; set; }

        [JsonProperty("gc_heap_bytes")]
        public long GcHeapBytes { get; set; }

        [JsonProperty("threadpool_thread_count")]
        public int ThreadPoolThreadCount { get; set; }

        [JsonProperty("threadpool_queue_length")]
        public long ThreadPoolQueueLength { get; set; }

        [JsonProperty("cpu_percent")]
        public double CpuPercent { get; set; }

        [JsonProperty("memory_bytes")]
        public long MemoryBytes { get; set; }

        /// <summary>
        /// Assembles a dashboard-schema record from the per-operation window rollup, the
        /// per-window runtime metrics snapshot, and the static per-run context.
        /// </summary>
        public static PerfResultsRecord Build(
            DateTime timestampUtc,
            PerfRunContext runContext,
            OperationWindowSnapshot window,
            RuntimeMetricsSnapshot runtime)
        {
            if (runContext == null)
            {
                throw new ArgumentNullException(nameof(runContext));
            }

            return new PerfResultsRecord
            {
                Timestamp = timestampUtc,
                Operation = runContext.Operation,
                P50Ms = window.P50Ms,
                P90Ms = window.P90Ms,
                P99Ms = window.P99Ms,
                MeanMs = window.MeanMs,
                Count = window.Count,
                Errors = window.Errors,
                ErrorMessage = window.ErrorMessage,
                ErrorStatusCode = window.ErrorStatusCode,
                Hostname = runContext.Hostname,
                SdkVersion = runContext.SdkVersion,
                CommitSha = runContext.CommitSha,
                ConfigConcurrency = runContext.ConfigConcurrency,
                ConfigApplicationRegion = runContext.ConfigApplicationRegion,
                RuPerSec = window.RuPerSec,
                GcGen0Count = runtime.GcGen0Count,
                GcGen1Count = runtime.GcGen1Count,
                GcGen2Count = runtime.GcGen2Count,
                GcHeapBytes = runtime.GcHeapBytes,
                ThreadPoolThreadCount = runtime.ThreadPoolThreadCount,
                ThreadPoolQueueLength = runtime.ThreadPoolQueueLength,
                CpuPercent = runtime.CpuPercent,
                MemoryBytes = runtime.MemoryBytes,
            };
        }
    }

    /// <summary>
    /// Static, per-run context stamped onto every <see cref="PerfResultsRecord"/>.
    /// </summary>
    internal sealed class PerfRunContext
    {
        public string Operation { get; set; }

        public string Hostname { get; set; }

        public string SdkVersion { get; set; }

        public string CommitSha { get; set; }

        public int ConfigConcurrency { get; set; }

        public string ConfigApplicationRegion { get; set; }
    }
}
