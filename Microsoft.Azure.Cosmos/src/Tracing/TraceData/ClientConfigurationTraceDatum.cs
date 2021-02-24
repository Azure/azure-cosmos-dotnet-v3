// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    internal sealed class ClientConfigurationTraceDatum : TraceDatum
    {
        public ClientConfigurationTraceDatum(CosmosClientContext cosmosClientContext, DateTime startTime)
        {
            this.ClientCreatedDateTimeUtc = startTime;
            this.ConnectionConfig = new Dictionary<string, string>();
            this.RecordClientConfig(cosmosClientContext);
        }

        internal DateTime ClientCreatedDateTimeUtc { get; }

        internal int NumberOfClients { get; set; }

        internal Dictionary<string, string> ConnectionConfig { get; set; }
        
        internal string ConsistencyConfig { get; set; }

        internal override void Accept(ITraceDatumVisitor traceDatumVisitor)
        {
            traceDatumVisitor.Visit(this);
        }

        private void RecordClientConfig(CosmosClientContext cosmosClientContext)
        {
            this.ConnectionConfig["gw"] = string.Format(CultureInfo.InvariantCulture,
                                                "(cps:{0}, rto:{1}, p:{2})",
                                                cosmosClientContext.DocumentClient.ConnectionPolicy.MaxConnectionLimit,
                                                (int)cosmosClientContext.DocumentClient.ConnectionPolicy.RequestTimeout.TotalSeconds,
                                                cosmosClientContext.ClientOptions.WebProxy != null);

            cosmosClientContext.DocumentClient.RecordTcpSettings(this);

            this.ConnectionConfig["other"] = string.Format(CultureInfo.InvariantCulture,
                                                "(ed:{0}, be:{1})",
                                                cosmosClientContext.DocumentClient.ConnectionPolicy.EnableEndpointDiscovery,
                                                cosmosClientContext.ClientOptions.AllowBulkExecution);

            this.ConsistencyConfig = string.Format(CultureInfo.InvariantCulture,
                                    "(consistency: {0}, mm: {1}, prgns:['{2}'])",
                                    cosmosClientContext.ClientOptions.ConsistencyLevel.GetValueOrDefault(),
                                    cosmosClientContext.DocumentClient.ConnectionPolicy.UseMultipleWriteLocations,
                                    this.PreferredRegionsInternal(cosmosClientContext.ClientOptions.ApplicationPreferredRegions));

            this.NumberOfClients = CosmosClient.numberOfClients;
        }

        private string PreferredRegionsInternal(IReadOnlyList<string> applicationPreferredRegions)
        {
            if (applicationPreferredRegions == null)
            {
                return string.Empty;
            }

            return string.Join(", ", applicationPreferredRegions);
        }
    }
}
