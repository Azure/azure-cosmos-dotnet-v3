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
                writer.WriteString(JsonEncodedStrings.CosmosSerializer, cosmosJsonSerializerWrapper.InternalJsonSerializer.GetType().ToString());
            }

            CosmosSerializer cosmosSerializer = value.Serializer as CosmosSerializer;
            if (cosmosSerializer is CosmosSerializer)
            {
                writer.WriteString(JsonEncodedStrings.CosmosSerializer, cosmosSerializer.GetType().ToString());
            }

            if (!string.IsNullOrEmpty(value.ApplicationId))
            {
                writer.WriteString(JsonEncodedStrings.ApplicationName, value.ApplicationId);
            }

            if (value.GatewayModeMaxConnectionLimit > 0)
            {
                writer.WriteNumber(JsonEncodedStrings.GatewayModeMaxConnectionLimit, value.GatewayModeMaxConnectionLimit);
            }

            if (value.RequestTimeout != null)
            {
                writer.WriteString(JsonEncodedStrings.RequestTimeout, value.RequestTimeout.ToString());
            }

            writer.WriteString(JsonEncodedStrings.ConnectionMode, value.ConnectionMode.ToString());

            if (value.ConsistencyLevel.HasValue)
            {
                writer.WriteString(JsonEncodedStrings.ConsistencyLevel, value.ConsistencyLevel.Value.ToString());
            }

            if (value.MaxRetryAttemptsOnRateLimitedRequests.HasValue)
            {
                writer.WriteNumber(JsonEncodedStrings.MaxRetryAttemptsOnRateLimitedRequests, value.MaxRetryAttemptsOnRateLimitedRequests.Value);
            }

            if (value.MaxRetryWaitTimeOnRateLimitedRequests.HasValue)
            {
                writer.WriteString(JsonEncodedStrings.MaxRetryWaitTimeOnRateLimitedRequests, value.MaxRetryWaitTimeOnRateLimitedRequests.Value.ToString());
            }

            if (value.IdleTcpConnectionTimeout.HasValue)
            {
                writer.WriteString(JsonEncodedStrings.IdleTcpConnectionTimeout, value.IdleTcpConnectionTimeout.Value.ToString());
            }

            if (value.OpenTcpConnectionTimeout.HasValue)
            {
                writer.WriteString(JsonEncodedStrings.OpenTcpConnectionTimeout, value.OpenTcpConnectionTimeout.Value.ToString());
            }

            if (value.MaxRequestsPerTcpConnection.HasValue)
            {
                writer.WriteNumber(JsonEncodedStrings.MaxRequestsPerTcpConnection, value.MaxRequestsPerTcpConnection.Value);
            }

            if (value.MaxTcpConnectionsPerEndpoint.HasValue)
            {
                writer.WriteNumber(JsonEncodedStrings.MaxTcpConnectionsPerEndpoint, value.MaxTcpConnectionsPerEndpoint.Value);
            }

            if (value.LimitToEndpoint)
            {
                writer.WriteBoolean(JsonEncodedStrings.LimitToEndpoint, value.LimitToEndpoint);
            }

            if (value.AllowBulkExecution)
            {
                writer.WriteBoolean(JsonEncodedStrings.AllowBulkExecution, value.AllowBulkExecution);
            }

            writer.WriteEndObject();
        }
    }
}
