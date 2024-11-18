//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    internal sealed class AppInsightClassicAttributeKeys : IActivityAttributePopulator
    {
        /// <summary>
        /// Represents the diagnostic namespace for Azure Cosmos.
        /// </summary>
        public const string DbName = "db.name";

        /// <summary>
        /// Represents the name of the database operation.
        /// </summary>
        public const string DbOperation = "db.operation";

        /// <summary>
        /// Represents the server address.
        /// </summary>
        public const string ServerAddress = "net.peer.name";

        /// <summary>
        /// Represents the name of the container in Cosmos DB.
        /// </summary>
        public const string ContainerName = "db.cosmosdb.container";

        /// <summary>
        /// Represents the status code of the response.
        /// </summary>
        public const string StatusCode = "db.cosmosdb.status_code";

        /// <summary>
        /// Represents the user agent
        /// </summary>
        public const string UserAgent = "db.cosmosdb.user_agent";

        /// <summary>
        /// Represents the machine ID for Cosmos DB.
        /// </summary>
        public const string MachineId = "db.cosmosdb.machine_id";

        /// <summary>
        /// Represents the type of operation for Cosmos DB.
        /// </summary>
        public const string OperationType = "db.cosmosdb.operation_type";

        /// <summary>
        /// Represents the sub-status code of the response.
        /// </summary>
        public const string SubStatusCode = "db.cosmosdb.sub_status_code";

        /// <summary>
        /// Represents the content length of the request.
        /// </summary>
        public const string RequestContentLength = "db.cosmosdb.request_content_length_bytes";

        /// <summary>
        /// Represents the content length of the response.
        /// </summary>
        public const string ResponseContentLength = "db.cosmosdb.response_content_length_bytes";

        /// <summary>
        /// Represents the client ID for Cosmos DB.
        /// </summary>
        public const string ClientId = "db.cosmosdb.client_id";

        /// <summary>
        /// Represents the request charge for the operation.
        /// </summary>
        public const string RequestCharge = "db.cosmosdb.request_charge";

        /// <summary>
        /// Represents the activity ID for the operation.
        /// </summary>
        public const string ActivityId = "db.cosmosdb.activity_id";

        /// <summary>
        /// Represents the connection mode for Cosmos DB.
        /// </summary>
        public const string ConnectionMode = "db.cosmosdb.connection_mode";

        /// <summary>
        /// Represents the regions contacted for the operation.
        /// </summary>
        public const string Region = "db.cosmosdb.regions_contacted";

        /// <summary>
        /// Represents the item count in the operation.
        /// </summary>
        public const string ItemCount = "db.cosmosdb.item_count";

        /// <summary>
        /// Represents the type of exception.
        /// </summary>
        public const string ExceptionType = "exception.type";

        /// <summary>
        /// Represents the message of the exception.
        /// </summary>
        public const string ExceptionMessage = "exception.message";

        /// <summary>
        /// Represents the stack trace of the exception.
        /// </summary>
        public const string ExceptionStacktrace = "exception.stacktrace";

        public void PopulateAttributes(DiagnosticScope scope,
            string operationName,
            string databaseName,
            string containerName,
            Uri accountName,
            string userAgent,
            string machineId,
            string clientId,
            string connectionMode)
        {
            scope.AddAttribute(AppInsightClassicAttributeKeys.DbOperation, operationName);
            scope.AddAttribute(AppInsightClassicAttributeKeys.DbName, databaseName);
            scope.AddAttribute(AppInsightClassicAttributeKeys.ContainerName, containerName);
            scope.AddAttribute(AppInsightClassicAttributeKeys.ServerAddress, accountName?.Host);
            scope.AddAttribute(AppInsightClassicAttributeKeys.UserAgent, userAgent);
            scope.AddAttribute(AppInsightClassicAttributeKeys.MachineId, machineId);
            scope.AddAttribute(AppInsightClassicAttributeKeys.ClientId, clientId);
            scope.AddAttribute(AppInsightClassicAttributeKeys.ConnectionMode, connectionMode);
        }

        public void PopulateAttributes(DiagnosticScope scope, Exception exception)
        {
            scope.AddAttribute(AppInsightClassicAttributeKeys.ExceptionStacktrace, exception.StackTrace);
            scope.AddAttribute(AppInsightClassicAttributeKeys.ExceptionType, exception.GetType().Name);

            // If Exception is not registered with open Telemetry
            if (!OpenTelemetryCoreRecorder.IsExceptionRegistered(exception, scope))
            {
                scope.AddAttribute(AppInsightClassicAttributeKeys.ExceptionMessage, exception.Message);
            }
        }

        public void PopulateAttributes(DiagnosticScope scope, QueryTextMode? queryTextMode, string operationType, OpenTelemetryAttributes response)
        {
            scope.AddAttribute(AppInsightClassicAttributeKeys.OperationType, operationType);
            if (response != null)
            {
                scope.AddAttribute(AppInsightClassicAttributeKeys.RequestContentLength, response.RequestContentLength);
                scope.AddAttribute(AppInsightClassicAttributeKeys.ResponseContentLength, response.ResponseContentLength);
                scope.AddIntegerAttribute(AppInsightClassicAttributeKeys.StatusCode, Convert.ToInt32(response.StatusCode));
                scope.AddIntegerAttribute(AppInsightClassicAttributeKeys.SubStatusCode, response.SubStatusCode);
                scope.AddIntegerAttribute(AppInsightClassicAttributeKeys.RequestCharge, Convert.ToInt32(response.RequestCharge));
                scope.AddAttribute(AppInsightClassicAttributeKeys.ItemCount, response.ItemCount);
                scope.AddAttribute(AppInsightClassicAttributeKeys.ActivityId, response.ActivityId);

                if (response.Diagnostics != null)
                {
                    scope.AddAttribute(AppInsightClassicAttributeKeys.Region, ClientTelemetryHelper.GetContactedRegions(response.Diagnostics.GetContactedRegions()));
                }
            }
        }

        public KeyValuePair<string, object>[] PopulateNetworkMeterDimensions(string operationName, Uri accountName, string containerName, string databaseName, OpenTelemetryAttributes attributes, CosmosException ex, ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics tcpStats = null, ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics? httpStats = null)
        {
            return new KeyValuePair<string, object>[]
            {
                new KeyValuePair<string, object>(AppInsightClassicAttributeKeys.ContainerName, containerName),
                new KeyValuePair<string, object>(AppInsightClassicAttributeKeys.DbName, databaseName),
                new KeyValuePair<string, object>(AppInsightClassicAttributeKeys.ServerAddress, accountName?.Host),
                new KeyValuePair<string, object>(AppInsightClassicAttributeKeys.DbOperation, operationName),
                new KeyValuePair<string, object>(AppInsightClassicAttributeKeys.StatusCode, (int)(attributes?.StatusCode ?? ex?.StatusCode)),
                new KeyValuePair<string, object>(AppInsightClassicAttributeKeys.SubStatusCode, attributes?.SubStatusCode ?? ex?.SubStatusCode)
            };
        }

        public KeyValuePair<string, object>[] PopulateOperationMeterDimensions(string operationName, string containerName, string databaseName, Uri accountName, OpenTelemetryAttributes attributes, CosmosException ex)
        {
            return new KeyValuePair<string, object>[]
            {
                new KeyValuePair<string, object>(AppInsightClassicAttributeKeys.ContainerName, containerName),
                new KeyValuePair<string, object>(AppInsightClassicAttributeKeys.DbName, databaseName),
                new KeyValuePair<string, object>(AppInsightClassicAttributeKeys.ServerAddress, accountName?.Host),
                new KeyValuePair<string, object>(AppInsightClassicAttributeKeys.DbOperation, operationName),
                new KeyValuePair<string, object>(AppInsightClassicAttributeKeys.StatusCode, (int)(attributes?.StatusCode ?? ex?.StatusCode)),
                new KeyValuePair<string, object>(AppInsightClassicAttributeKeys.SubStatusCode, attributes?.SubStatusCode ?? ex?.SubStatusCode)
            };
        }
    }
}
