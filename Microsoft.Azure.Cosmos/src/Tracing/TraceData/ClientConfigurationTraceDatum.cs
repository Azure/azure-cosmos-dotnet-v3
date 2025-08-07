// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Telemetry;
    using System.Text.Json;
    using System.Buffers;

    internal sealed class ClientConfigurationTraceDatum : TraceDatum
    {
        public ClientConfigurationTraceDatum(CosmosClientContext cosmosClientContext, DateTime startTime)
        {
            this.ClientCreatedDateTimeUtc = startTime;
            this.UserAgentContainer = cosmosClientContext.DocumentClient.ConnectionPolicy.UserAgentContainer;
            this.GatewayConnectionConfig = new GatewayConnectionConfig(cosmosClientContext.ClientOptions.GatewayModeMaxConnectionLimit,
                                                                       cosmosClientContext.ClientOptions.RequestTimeout,
                                                                       cosmosClientContext.ClientOptions.WebProxy,
                                                                       cosmosClientContext.ClientOptions.HttpClientFactory);

#if !COSMOS_GW_AOT
            this.RntbdConnectionConfig = cosmosClientContext.DocumentClient.RecordTcpSettings(this);
#endif

            this.OtherConnectionConfig = new OtherConnectionConfig(cosmosClientContext.ClientOptions.LimitToEndpoint,
                                                cosmosClientContext.ClientOptions.AllowBulkExecution);

            this.ConsistencyConfig = new ConsistencyConfig(cosmosClientContext.ClientOptions.ConsistencyLevel,
                                                    cosmosClientContext.ClientOptions.ApplicationPreferredRegions, cosmosClientContext.ClientOptions.ApplicationRegion);

            this.cachedNumberOfClientCreated = CosmosClient.numberOfClientsCreated;
            this.cachedNumberOfActiveClient = CosmosClient.NumberOfActiveClients;
            this.cachedUserAgentString = this.UserAgentContainer.UserAgent;
            this.cachedMachineId = VmMetadataApiHandler.GetMachineId();
            this.ProcessorCount = Environment.ProcessorCount;
            this.ConnectionMode = cosmosClientContext.ClientOptions.ConnectionMode;
            this.cachedVMRegion = VmMetadataApiHandler.GetMachineRegion();
            this.cachedSerializedJson = this.GetSerializedDatum();
        }

        public DateTime ClientCreatedDateTimeUtc { get; }

        public GatewayConnectionConfig GatewayConnectionConfig { get; }

#if !COSMOS_GW_AOT
        public RntbdConnectionConfig RntbdConnectionConfig { get; }
#endif

        public OtherConnectionConfig OtherConnectionConfig { get; }

        public ConsistencyConfig ConsistencyConfig { get; }

        public int ProcessorCount { get; }

        public ConnectionMode ConnectionMode { get; }

        public ReadOnlyMemory<byte> SerializedJson
        {
            get
            {
                if (this.cachedUserAgentString != this.UserAgentContainer.UserAgent ||
                    this.cachedNumberOfClientCreated != CosmosClient.numberOfClientsCreated ||
                    this.cachedNumberOfActiveClient != CosmosClient.NumberOfActiveClients ||
                    !ReferenceEquals(this.cachedMachineId, VmMetadataApiHandler.GetMachineId()) ||
                    !ReferenceEquals(this.cachedVMRegion, VmMetadataApiHandler.GetMachineRegion()))
                {
                    this.cachedNumberOfActiveClient = CosmosClient.NumberOfActiveClients;
                    this.cachedNumberOfClientCreated = CosmosClient.numberOfClientsCreated;
                    this.cachedUserAgentString = this.UserAgentContainer.UserAgent;
                    this.cachedMachineId = VmMetadataApiHandler.GetMachineId();
                    this.cachedVMRegion = VmMetadataApiHandler.GetMachineRegion();
                    this.cachedSerializedJson = this.GetSerializedDatum();
                }

                return this.cachedSerializedJson;
            }
        }

        internal readonly UserAgentContainer UserAgentContainer;

        private ReadOnlyMemory<byte> cachedSerializedJson;
        private int cachedNumberOfClientCreated;
        private int cachedNumberOfActiveClient;

        private string cachedUserAgentString;

        private string cachedMachineId;
        private string cachedVMRegion;

        internal override void Accept(ITraceDatumVisitor traceDatumVisitor)
        {
            traceDatumVisitor.Visit(this);
        }

        private ReadOnlyMemory<byte> GetSerializedDatum()
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();

                writer.WriteString("Client Created Time Utc", this.ClientCreatedDateTimeUtc.ToString("o", CultureInfo.InvariantCulture));
                writer.WriteString("MachineId", this.cachedMachineId);
                if (this.cachedVMRegion != null)
                {
                    writer.WriteString("VM Region", this.cachedVMRegion);
                }
                writer.WriteNumber("NumberOfClientsCreated", this.cachedNumberOfClientCreated);
                writer.WriteNumber("NumberOfActiveClients", this.cachedNumberOfActiveClient);
                writer.WriteString("ConnectionMode", this.ConnectionMode.ToString());
                writer.WriteString("User Agent", this.cachedUserAgentString);

                writer.WritePropertyName("ConnectionConfig");
                writer.WriteStartObject();

                writer.WriteString("gw", this.GatewayConnectionConfig.ToString());
#if !COSMOS_GW_AOT
                writer.WriteString("rntbd", this.RntbdConnectionConfig.ToString());
#endif
                writer.WriteString("other", this.OtherConnectionConfig.ToString());

                writer.WriteEndObject();

                writer.WriteString("ConsistencyConfig", this.ConsistencyConfig.ToString());
                writer.WriteNumber("ProcessorCount", this.ProcessorCount);

                writer.WriteEndObject();
            }

            return buffer.WrittenMemory;
        }
    }
}
