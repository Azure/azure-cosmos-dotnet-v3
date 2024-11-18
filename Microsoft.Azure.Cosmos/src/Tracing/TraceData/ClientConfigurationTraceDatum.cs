// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Telemetry;

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

            this.RntbdConnectionConfig = cosmosClientContext.DocumentClient.RecordTcpSettings(this);

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

        public RntbdConnectionConfig RntbdConnectionConfig { get; }

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
            IJsonWriter jsonTextWriter = JsonWriter.Create(JsonSerializationFormat.Text);
            jsonTextWriter.WriteObjectStart();

            jsonTextWriter.WriteFieldName("Client Created Time Utc");
            jsonTextWriter.WriteStringValue(this.ClientCreatedDateTimeUtc.ToString("o", CultureInfo.InvariantCulture));
            jsonTextWriter.WriteFieldName("MachineId");
            jsonTextWriter.WriteStringValue(this.cachedMachineId);
            if (this.cachedVMRegion != null)
            {
                jsonTextWriter.WriteFieldName("VM Region");
                jsonTextWriter.WriteStringValue(this.cachedVMRegion);
            }
            jsonTextWriter.WriteFieldName("NumberOfClientsCreated");
            jsonTextWriter.WriteNumberValue(this.cachedNumberOfClientCreated);
            jsonTextWriter.WriteFieldName("NumberOfActiveClients");
            jsonTextWriter.WriteNumberValue(this.cachedNumberOfActiveClient);
            jsonTextWriter.WriteFieldName("ConnectionMode");
            jsonTextWriter.WriteStringValue(this.ConnectionMode.ToString());
            jsonTextWriter.WriteFieldName("User Agent");
            jsonTextWriter.WriteStringValue(this.cachedUserAgentString);

            jsonTextWriter.WriteFieldName("ConnectionConfig");
            jsonTextWriter.WriteObjectStart();

            jsonTextWriter.WriteFieldName("gw");
            jsonTextWriter.WriteStringValue(this.GatewayConnectionConfig.ToString());
            jsonTextWriter.WriteFieldName("rntbd");
            jsonTextWriter.WriteStringValue(this.RntbdConnectionConfig.ToString());
            jsonTextWriter.WriteFieldName("other");
            jsonTextWriter.WriteStringValue(this.OtherConnectionConfig.ToString());

            jsonTextWriter.WriteObjectEnd();

            jsonTextWriter.WriteFieldName("ConsistencyConfig");
            jsonTextWriter.WriteStringValue(this.ConsistencyConfig.ToString());
            jsonTextWriter.WriteFieldName("ProcessorCount");
            jsonTextWriter.WriteNumberValue(this.ProcessorCount);

            jsonTextWriter.WriteObjectEnd();

            return jsonTextWriter.GetResult();
        }
    }
}
