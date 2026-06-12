//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Azure.Core;
    using Azure.Identity;
    using Kusto.Data;
    using Kusto.Data.Common;
    using Kusto.Data.Ingestion;
    using Kusto.Ingest;
    using Newtonsoft.Json;

    /// <summary>
    /// Routes per-window <see cref="PerfResultsRecord"/> rows to an Azure Data Explorer (Kusto)
    /// table that the dedicated .NET perf Grafana dashboard reads (database <c>DotNetPerf</c>,
    /// table <c>PerfResults</c>). Authenticates with AAD / managed identity by default
    /// (no committed keys; SE-5).
    /// </summary>
    internal sealed class AzureDataExplorerMetricsSink : IMetricsSink
    {
        private readonly IKustoIngestClient ingestClient;
        private readonly string databaseName;
        private readonly string tableName;
        private readonly IngestionMapping ingestionMapping;

        public AzureDataExplorerMetricsSink(BenchmarkConfig config)
        {
            this.databaseName = config.AdxMetricsDatabase;
            this.tableName = config.AdxMetricsTable;

            TokenCredential credential = string.IsNullOrWhiteSpace(config.AdxManagedIdentityClientId)
                ? new DefaultAzureCredential()
                : new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ManagedIdentityClientId = config.AdxManagedIdentityClientId
                });

            KustoConnectionStringBuilder kcsb = new KustoConnectionStringBuilder(config.AdxMetricsUri)
                .WithAadAzureTokenCredentialsAuthentication(credential);

            this.ingestClient = KustoIngestFactory.CreateQueuedIngestClient(kcsb);
            this.ingestionMapping = new IngestionMapping
            {
                IngestionMappingKind = IngestionMappingKind.Json,
                IngestionMappings = BuildColumnMappings(),
            };

            Utility.TeeTraceInformation(
                $"AzureDataExplorerMetricsSink enabled -> {config.AdxMetricsUri} db={this.databaseName} table={this.tableName}");
        }

        public async Task EmitAsync(IReadOnlyList<PerfResultsRecord> records)
        {
            if (records == null || records.Count == 0)
            {
                return;
            }

            try
            {
                StringBuilder builder = new StringBuilder();
                foreach (PerfResultsRecord record in records)
                {
                    // Newline-delimited JSON: one record per line (multijson stream).
                    builder.AppendLine(JsonConvert.SerializeObject(record));
                }

                byte[] payload = Encoding.UTF8.GetBytes(builder.ToString());
                using MemoryStream stream = new MemoryStream(payload);

                KustoQueuedIngestionProperties ingestionProperties = new KustoQueuedIngestionProperties(this.databaseName, this.tableName)
                {
                    Format = DataSourceFormat.multijson,
                    IngestionMapping = this.ingestionMapping,
                    FlushImmediately = true,
                };

                await this.ingestClient.IngestFromStreamAsync(
                    stream,
                    ingestionProperties,
                    new StreamSourceOptions { LeaveOpen = true });
            }
            catch (Exception ex)
            {
                // A telemetry-sink failure must never interrupt the benchmark run.
                Utility.TeeTraceInformation("AzureDataExplorerMetricsSink emit failed: " + ex);
            }
        }

        public Task FlushAsync()
        {
            try
            {
                this.ingestClient?.Dispose();
            }
            catch (Exception ex)
            {
                Utility.TeeTraceInformation("AzureDataExplorerMetricsSink flush failed: " + ex);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Builds the JSON ingestion mapping from the <see cref="PerfResultsRecord"/>
        /// <see cref="JsonPropertyAttribute"/> names so the mapping stays in sync with the schema.
        /// </summary>
        private static List<ColumnMapping> BuildColumnMappings()
        {
            List<ColumnMapping> mappings = new List<ColumnMapping>();
            foreach (PropertyInfo property in typeof(PerfResultsRecord).GetProperties())
            {
                JsonPropertyAttribute attribute = property.GetCustomAttribute<JsonPropertyAttribute>();
                string columnName = attribute?.PropertyName ?? property.Name;
                mappings.Add(new ColumnMapping
                {
                    ColumnName = columnName,
                    Properties = new Dictionary<string, string>
                    {
                        { MappingConsts.Path, "$." + columnName },
                    },
                });
            }

            return mappings;
        }
    }
}
