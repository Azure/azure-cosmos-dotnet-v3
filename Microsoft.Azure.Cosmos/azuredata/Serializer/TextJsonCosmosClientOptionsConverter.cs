//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Cosmos.Serialization;

    internal sealed class TextJsonCosmosClientOptionsConverter : JsonConverter<CosmosClientOptions>
    {
        public override CosmosClientOptions Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(
            Utf8JsonWriter writer,
            CosmosClientOptions value,
            JsonSerializerOptions options)
        {
            if (value == null)
            {
                return;
            }

            writer.WriteStartObject();

            CosmosJsonSerializerWrapper cosmosJsonSerializerWrapper = value.Serializer as CosmosJsonSerializerWrapper;
            if (value.Serializer is CosmosJsonSerializerWrapper)
            {
                writer.WriteString("CosmosSerializer", cosmosJsonSerializerWrapper.InternalJsonSerializer.GetType().ToString());
            }

            CosmosSerializer cosmosSerializer = value.Serializer as CosmosSerializer;
            if (cosmosSerializer is CosmosSerializer)
            {
                writer.WriteString("CosmosSerializer", cosmosSerializer.GetType().ToString());
            }

            if (!string.IsNullOrEmpty(value.ApplicationName))
            {
                writer.WriteString("ApplicationName", value.ApplicationName);
            }

            if (value.GatewayModeMaxConnectionLimit > 0)
            {
                writer.WriteNumber("GatewayModeMaxConnectionLimit", value.GatewayModeMaxConnectionLimit);
            }

            if (value.RequestTimeout != null)
            {
                writer.WriteString("RequestTimeout", value.RequestTimeout.ToString());
            }

            writer.WriteString("ConnectionMode", JsonSerializer.Serialize(value.ConnectionMode));

            if (value.ConsistencyLevel.HasValue)
            {
                writer.WriteString("ConsistencyLevel", JsonSerializer.Serialize(value.ConsistencyLevel.Value));
            }

            if (value.MaxRetryAttemptsOnRateLimitedRequests.HasValue)
            {
                writer.WriteNumber("MaxRetryAttemptsOnRateLimitedRequests", value.MaxRetryAttemptsOnRateLimitedRequests.Value);
            }

            if (value.MaxRetryWaitTimeOnRateLimitedRequests.HasValue)
            {
                writer.WriteString("MaxRetryWaitTimeOnRateLimitedRequests", value.MaxRetryWaitTimeOnRateLimitedRequests.Value.ToString());
            }

            if (value.IdleTcpConnectionTimeout.HasValue)
            {
                writer.WriteString("IdleTcpConnectionTimeout", value.IdleTcpConnectionTimeout.Value.ToString());
            }

            if (value.OpenTcpConnectionTimeout.HasValue)
            {
                writer.WriteString("MaxRetryWaitTimeOnRateLimitedRequests", value.OpenTcpConnectionTimeout.Value.ToString());
            }

            if (value.MaxRequestsPerTcpConnection.HasValue)
            {
                writer.WriteNumber("MaxRequestsPerTcpConnection", value.MaxRequestsPerTcpConnection.Value);
            }

            if (value.MaxTcpConnectionsPerEndpoint.HasValue)
            {
                writer.WriteNumber("MaxTcpConnectionsPerEndpoint", value.MaxTcpConnectionsPerEndpoint.Value);
            }

            if (value.LimitToEndpoint)
            {
                writer.WriteBoolean("LimitToEndpoint", value.LimitToEndpoint);
            }

            writer.WriteEndObject();
        }
    }
}
