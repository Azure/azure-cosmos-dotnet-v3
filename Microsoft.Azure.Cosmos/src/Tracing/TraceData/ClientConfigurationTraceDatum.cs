// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Json;

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
                                                    cosmosClientContext.ClientOptions.ApplicationPreferredRegions);

            this.cachedNumberOfClientCreated = CosmosClient.numberOfClientsCreated;
            this.cachedUserAgentString = this.UserAgentContainer.UserAgent;
            this.cachedSerializedJson = this.GetSerializedDatum();
        }

        public DateTime ClientCreatedDateTimeUtc { get; }

        public GatewayConnectionConfig GatewayConnectionConfig { get; }

        public RntbdConnectionConfig RntbdConnectionConfig { get; }

        public OtherConnectionConfig OtherConnectionConfig { get; }

        public ConsistencyConfig ConsistencyConfig { get; }

        public ReadOnlyMemory<byte> SerializedJson
        {
            get
            {
                if ((this.cachedUserAgentString != this.UserAgentContainer.UserAgent) ||
                    (this.cachedNumberOfClientCreated != CosmosClient.numberOfClientsCreated))
                {
                    this.cachedNumberOfClientCreated = CosmosClient.numberOfClientsCreated;
                    this.cachedUserAgentString = this.UserAgentContainer.UserAgent;
                    this.cachedSerializedJson = this.GetSerializedDatum();
                }

                return this.cachedSerializedJson;
            }
        }

        internal readonly UserAgentContainer UserAgentContainer;

        private ReadOnlyMemory<byte> cachedSerializedJson;
        private int cachedNumberOfClientCreated;
        private string cachedUserAgentString;

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

            jsonTextWriter.WriteFieldName("NumberOfClientsCreated");
            jsonTextWriter.WriteNumber64Value(this.cachedNumberOfClientCreated);
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
            jsonTextWriter.WriteObjectEnd();

            return jsonTextWriter.GetResult();
        }
    }
}
