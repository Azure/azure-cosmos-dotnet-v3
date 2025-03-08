//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Contains constant string values representing OpenTelemetry attribute keys for monitoring and tracing Cosmos DB operations.
    /// These keys follow the OpenTelemetry conventions and the Cosmos DB semantic conventions as outlined in the OpenTelemetry specification.
    /// </summary>
    /// <remarks>
    /// For more details on the semantic conventions, refer to the OpenTelemetry documentation at:
    /// <see href="https://opentelemetry.io/docs/specs/semconv/database/cosmosdb/"/> OpenTelemetry Semantic Conventions 1.28.0 conventions are followed.
    /// </remarks>
    internal sealed class OpenTelemetryAttributeKeys : IActivityAttributePopulator
    {
        private readonly NetworkMetricsOptions networkMetricsOptions;
        private readonly OperationMetricsOptions operationMetricsOptions;

        public OpenTelemetryAttributeKeys(OperationMetricsOptions operationMetricsOptions = null, NetworkMetricsOptions networkMetricsOptions = null)
        {
            this.networkMetricsOptions = networkMetricsOptions ?? new NetworkMetricsOptions();
            this.operationMetricsOptions = operationMetricsOptions ?? new OperationMetricsOptions();
        }
        
        // Azure defaults

        /// <summary>
        /// Represents the diagnostic namespace for Azure Cosmos.
        /// </summary>
        public const string DiagnosticNamespace = "Azure.Cosmos";

        /// <summary>
        /// Represents the resource provider namespace for Azure Cosmos.
        /// </summary>
        public const string ResourceProviderNamespace = "Microsoft.DocumentDB";

        /// <summary>
        /// Represents the prefix for operation names.
        /// </summary>
        public const string OperationPrefix = "Operation";

        /// <summary>
        /// Represents the prefix for network-level operations.
        /// </summary>
        public const string NetworkLevelPrefix = "Request";

        // Common database attributes

        /// <summary>
        /// Represents the name of the database system.
        /// </summary>
        public const string DbSystemName = "db.system.name";

        /// <summary>
        /// Represents the namespace of the database.
        /// </summary>
        public const string DbName = "db.namespace";

        /// <summary>
        /// Represents the name of the database operation.
        /// </summary>
        public const string DbOperation = "db.operation.name";

        /// <summary>
        /// Represents the server address.
        /// </summary>
        public const string ServerAddress = "server.address";

        /// <summary>
        /// Represents the server port.
        /// </summary>
        public const string ServerPort = "server.port";

        // Cosmos DB specific attributes

        /// <summary>
        /// Represents the client ID for Cosmos DB.
        /// </summary>
        public const string ClientId = "azure.cosmosdb.client.id";

        /// <summary>
        /// Represents the user agent, compliant with OpenTelemetry conventions.
        /// </summary>
        public const string UserAgent = "user_agent.original";

        /// <summary>
        /// Represents the connection mode for Cosmos DB.
        /// </summary>
        public const string ConnectionMode = "azure.cosmosdb.connection.mode";

        // Request/Response specifics

        /// <summary>
        /// Represents the name of the container in Cosmos DB.
        /// </summary>
        public const string ContainerName = "db.collection.name";

        /// <summary>
        /// Represents the content length of the request.
        /// </summary>
        public const string RequestContentLength = "azure.cosmosdb.request.body.size";

        /// <summary>
        /// Represents the content length of the response.
        /// </summary>
        public const string ResponseContentLength = "azure.cosmosdb.response.body.size";

        /// <summary>
        /// Represents the status code of the response.
        /// </summary>
        public const string StatusCode = "db.response.status_code";

        /// <summary>
        /// Represents the sub-status code of the response.
        /// </summary>
        public const string SubStatusCode = "azure.cosmosdb.response.sub_status_code";

        /// <summary>
        /// Represents the request charge for the operation.
        /// </summary>
        public const string RequestCharge = "azure.cosmosdb.request.request_charge";

        /// <summary>
        /// Represents the regions contacted for the operation.
        /// </summary>
        public const string Region = "azure.cosmosdb.contacted_regions";

        /// <summary>
        /// Represents the item count in the operation.
        /// </summary>
        public const string ItemCount = "azure.cosmosdb.row.count";

        /// <summary>
        /// Represents the activity ID for the operation.
        /// </summary>
        public const string ActivityId = "azure.cosmosdb.activity_id";

        /// <summary>
        /// Represents the correlated activity ID for the operation.
        /// </summary>
        public const string CorrelatedActivityId = "azure.cosmosdb.correlated_activity_id";

        /// <summary>
        /// Represents the Azure Cosmos DB SQL Query.
        /// </summary>
        public const string QueryText = "db.query.text";

        /// <summary>
        /// Represents the size of the batch operation.
        /// </summary>
        public const string BatchSize = "db.operation.batch.size";

        /// <summary>
        /// Consistency Level
        /// </summary>
        public const string ConsistencyLevel = "azure.cosmosdb.consistency.level";

        // Exceptions

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

        public const string NetworkProtocolName = "network.protocol.name";

        public const string ServiceEndpointHost = "network.protocol.host";

        public const string ServiceEndPointPort = "network.protocol.port";

        public const string ServiceEndpointStatusCode = "azure.cosmosdb.network.response.status_code";
        
        public const string ServiceEndpointSubStatusCode = "azure.cosmosdb.network.response.sub_status_code";
        
        public const string ServiceEndpointRegion = "cloud.region";
        
        public const string ServiceEndpointRoutingId = "azure.cosmosdb.network.routing_id ";

        /// <summary>
        /// Represents the type of error.
        /// </summary>
        public const string ErrorType = "error.type";

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
            scope.AddAttribute(OpenTelemetryAttributeKeys.DbOperation, operationName);
            scope.AddAttribute(OpenTelemetryAttributeKeys.DbName, databaseName);
            scope.AddAttribute(OpenTelemetryAttributeKeys.ContainerName, containerName);
            if (accountName != null)
            {
                scope.AddAttribute(OpenTelemetryAttributeKeys.ServerAddress, accountName.Host);
                scope.AddIntegerAttribute(OpenTelemetryAttributeKeys.ServerPort, accountName.Port);
            }
            scope.AddAttribute(OpenTelemetryAttributeKeys.UserAgent, userAgent);
            scope.AddAttribute(OpenTelemetryAttributeKeys.ClientId, clientId);
            scope.AddAttribute(OpenTelemetryAttributeKeys.ConnectionMode, connectionMode);
        }

        public void PopulateAttributes(DiagnosticScope scope, Exception exception)
        {
            scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionStacktrace, exception.StackTrace);
            scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionType, exception.GetType().Name);

            // If Exception is not registered with open Telemetry
            if (!OpenTelemetryCoreRecorder.IsExceptionRegistered(exception, scope))
            {
                scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionMessage, exception.Message);
            }
        }

        public void PopulateAttributes(DiagnosticScope scope, 
            QueryTextMode? queryTextMode, 
            string operationType, 
            OpenTelemetryAttributes response)
        {
            if (response == null)
            {
                return;
            }

            if (response.BatchSize is not null)
            {
                scope.AddIntegerAttribute(OpenTelemetryAttributeKeys.BatchSize, Convert.ToInt32(response.BatchSize));
            }

            scope.AddIntegerAttribute(OpenTelemetryAttributeKeys.StatusCode, Convert.ToInt32(response.StatusCode));
            scope.AddAttribute(OpenTelemetryAttributeKeys.RequestContentLength, response.RequestContentLength);
            scope.AddAttribute(OpenTelemetryAttributeKeys.ResponseContentLength, response.ResponseContentLength);
            scope.AddIntegerAttribute(OpenTelemetryAttributeKeys.SubStatusCode, response.SubStatusCode);
            scope.AddIntegerAttribute(OpenTelemetryAttributeKeys.RequestCharge, Convert.ToInt32(response.RequestCharge));
            scope.AddAttribute(OpenTelemetryAttributeKeys.ItemCount, response.ItemCount);
            scope.AddAttribute(OpenTelemetryAttributeKeys.ActivityId, response.ActivityId);
            scope.AddAttribute(OpenTelemetryAttributeKeys.CorrelatedActivityId, response.CorrelatedActivityId);
            scope.AddAttribute(OpenTelemetryAttributeKeys.ConsistencyLevel, response.ConsistencyLevel);

            if (response.QuerySpec is not null)
            {
                if (queryTextMode == QueryTextMode.All ||
                    (queryTextMode == QueryTextMode.ParameterizedOnly && response.QuerySpec.ShouldSerializeParameters()))
                {
                    scope.AddAttribute(OpenTelemetryAttributeKeys.QueryText, response.QuerySpec?.QueryText);
                }
            }

            if (response.Diagnostics != null)
            {
                scope.AddAttribute<string[]>(
                    OpenTelemetryAttributeKeys.Region, 
                    CosmosDbMeterUtil.GetRegions(response.Diagnostics), (input) => string.Join(",", input));
            }
         
        }

        public KeyValuePair<string, object>[] PopulateNetworkMeterDimensions(string operationName,
            Uri accountName,
            string containerName,
            string databaseName,
            OpenTelemetryAttributes attributes,
            Exception ex,
            NetworkMetricsOptions optionFromRequest,
            ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics tcpStats = null,
            ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics? httpStats = null)
        {
            Uri replicaEndpoint = GetEndpoint(tcpStats, httpStats);

            int? operationLevelStatusCode = CosmosDbMeterUtil.GetStatusCode(attributes, ex);
            int? operationLevelSubStatusCode = CosmosDbMeterUtil.GetSubStatusCode(attributes, ex);

            int? serviceEndpointStatusCode = GetStatusCode(tcpStats, httpStats);
            int? serviceEndpointSubStatusCode = GetSubStatusCode(tcpStats, httpStats);
            Exception serviceEndpointException = httpStats?.Exception ?? tcpStats?.StoreResult?.Exception;

            List<KeyValuePair<string, object>> dimensions = new ()
            {
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbSystemName, OpenTelemetryCoreRecorder.CosmosDb),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ContainerName, containerName),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbName, databaseName),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerAddress, accountName?.Host),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerPort, accountName?.Port),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbOperation, operationName),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.StatusCode, operationLevelStatusCode),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.SubStatusCode, operationLevelSubStatusCode),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ConsistencyLevel, GetConsistencyLevel(attributes, ex)),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.NetworkProtocolName, replicaEndpoint.Scheme),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServiceEndpointHost, replicaEndpoint.Host),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServiceEndPointPort, replicaEndpoint.Port),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServiceEndpointStatusCode, serviceEndpointStatusCode),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServiceEndpointSubStatusCode, serviceEndpointSubStatusCode),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServiceEndpointRegion, GetRegion(tcpStats, httpStats)),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ErrorType, GetErrorType(serviceEndpointException, serviceEndpointStatusCode, serviceEndpointSubStatusCode))
            };

            this.AddOptionalDimensions(optionFromRequest, tcpStats, httpStats, dimensions);

            return dimensions.ToArray();
        }

        private void AddOptionalDimensions(NetworkMetricsOptions optionFromRequest, 
            ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics tcpStats, 
            ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics? httpStats, 
            List<KeyValuePair<string, object>> dimensions)
        {
            // Add custom dimensions from networkMetricsOptions
            this.AddCustomDimensions(this.networkMetricsOptions?.CustomDimensions, dimensions);

            // Add custom dimensions from optionFromRequest
            this.AddCustomDimensions(optionFromRequest?.CustomDimensions, dimensions);

            // If IncludeRoutingId is set to true at either at client options or request options, then add the routing id to the dimensions
            if (this.ShouldIncludeRoutingId(this.networkMetricsOptions, optionFromRequest))
            {
                dimensions.Add(new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServiceEndpointRoutingId, GetRoutingId(tcpStats, httpStats)));
            }
        }

        private bool ShouldIncludeRoutingId(
            NetworkMetricsOptions globalOptions,
            NetworkMetricsOptions requestOptions)
        {
            return (requestOptions == null && globalOptions?.IncludeRoutingId == true) ||
                   (requestOptions?.IncludeRoutingId == true);
        }

        public KeyValuePair<string, object>[] PopulateOperationMeterDimensions(string operationName, 
            string containerName, 
            string databaseName, 
            Uri accountName, 
            OpenTelemetryAttributes attributes, 
            Exception ex,
            OperationMetricsOptions optionFromRequest)
        {
            int? statusCode = CosmosDbMeterUtil.GetStatusCode(attributes, ex);
            int? subStatusCode = CosmosDbMeterUtil.GetSubStatusCode(attributes, ex);

            List<KeyValuePair<string, object>> dimensions = new ()
            {
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbSystemName, OpenTelemetryCoreRecorder.CosmosDb),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ContainerName, containerName),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbName, databaseName),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerAddress, accountName?.Host),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerPort, accountName?.Port),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbOperation, operationName),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.StatusCode, statusCode),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.SubStatusCode, subStatusCode),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ConsistencyLevel, GetConsistencyLevel(attributes, ex)),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ErrorType, GetErrorType(ex, statusCode, subStatusCode))
            };

            this.AddOptionalDimensions(attributes, optionFromRequest, dimensions);

            return dimensions.ToArray();
        }

        private void AddOptionalDimensions(OpenTelemetryAttributes attributes, OperationMetricsOptions optionFromRequest, List<KeyValuePair<string, object>> dimensions)
        {
            // Add custom dimensions from operationMetricsOptions
            this.AddCustomDimensions(this.operationMetricsOptions?.CustomDimensions, dimensions);

            // Add custom dimensions from optionFromRequest
            this.AddCustomDimensions(optionFromRequest?.CustomDimensions, dimensions);

            // If IncludeRegion is set to true at either at client options or request options, then add the Region contacted information to the dimensions
            if (this.ShouldIncludeRegion(this.operationMetricsOptions, optionFromRequest))
            {
                dimensions.Add(new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.Region, CosmosDbMeterUtil.GetRegions(attributes?.Diagnostics)));
            }
        }

        private bool ShouldIncludeRegion(
           OperationMetricsOptions globalOptions,
           OperationMetricsOptions requestOptions)
        {
            return (requestOptions == null && globalOptions?.IncludeRegion == true) ||
                   (requestOptions?.IncludeRegion == true);
        }

        private void AddCustomDimensions(
            IDictionary<string, string> customDimensions,
            List<KeyValuePair<string, object>> dimensions)
        {
            if (customDimensions != null)
            {
                foreach (KeyValuePair<string, string> customDimension in customDimensions)
                {
                    dimensions.Add(new KeyValuePair<string, object>(customDimension.Key, customDimension.Value));
                }
            }
        }

        private static int? GetStatusCode(ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics tcpStats, ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics? httpStats)
        {
            return (int?)httpStats?.HttpResponseMessage?.StatusCode ?? (int?)tcpStats?.StoreResult?.StatusCode;
        }

        private static string GetConsistencyLevel(OpenTelemetryAttributes attributes,
           Exception ex)
        {
            return ex switch
            {
                CosmosException cosmosException => cosmosException.Headers.ConsistencyLevel,
                _ when attributes != null => attributes.ConsistencyLevel,
                _ => null
            };
        }

        private static Uri GetEndpoint(ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics tcpStats, ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics? httpStats)
        {
            return httpStats?.RequestUri ?? tcpStats?.StoreResult?.StorePhysicalAddress;
        }

        /// <returns>
        ///  Direct Mode: partitions/{partitionId}/replicas/{replicaId}
        ///  Gateway Mode:
        ///     a) If Partition Key Range Id is available, then it is the value of x-ms-documentdb-partitionkeyrangeid header
        ///     b) otherwise, it is the path of the request URI
        ///  </returns>
        private static string GetRoutingId(ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics tcpStats, ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics? httpStats)
        {
            if (tcpStats != null)
            {
                string path = tcpStats?.StoreResult?.StorePhysicalAddress?.AbsolutePath;
                if (path == null)
                {
                    return string.Empty;
                }

                int startIndex = path.IndexOf("/partitions/");
                return startIndex >= 0 ? path.Substring(startIndex) : string.Empty;
            }

            if (httpStats.HasValue && httpStats.Value.HttpResponseMessage != null && httpStats.Value.HttpResponseMessage.Headers != null)
            {
                if (httpStats.Value.HttpResponseMessage.Headers.TryGetValues("x-ms-documentdb-partitionkeyrangeid", out IEnumerable<string> pkrangeid))
                {
                    return string.Join(",", pkrangeid);
                }
                else
                {
                    return httpStats.Value.RequestUri?.AbsolutePath;
                }
            }

            return string.Empty;
        }

        private static string GetRegion(ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics tcpStats, ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics? httpStats)
        {
            return httpStats?.Region ?? tcpStats.Region;
        }

        /// <summary>
        /// Return the error.type dimension value based on the exception type, status code and sub status code.
        /// </summary>
        /// <param name="exception">Threw exception</param>
        /// <param name="statusCode">Status code</param>
        /// <param name="subStatusCode">Sub status code</param>
        /// <returns>error.type dimension value</returns>
        private static string GetErrorType(Exception exception, int? statusCode, int? subStatusCode)
        {
            if (exception == null)
            {
                return null;
            }

            HttpStatusCode? code = statusCode.HasValue ? (HttpStatusCode)statusCode.Value : null;
            SubStatusCodes? subCode = subStatusCode.HasValue ? (SubStatusCodes)subStatusCode.Value : null;

            return $"{exception.GetType().Name}_{code?.ToString()}_{subCode?.ToString()}";
        }

        private static int GetSubStatusCode(ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics tcpStats, ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics? httpStats)
        {
            int? subStatuscode = null;
            if (httpStats.HasValue &&
                httpStats.Value.HttpResponseMessage?.Headers != null &&
                httpStats.Value
                    .HttpResponseMessage
                    .Headers
                    .TryGetValues(Documents.WFConstants.BackendHeaders.SubStatus, out IEnumerable<string> statuscodes))
            {
                subStatuscode = Convert.ToInt32(statuscodes.FirstOrDefault<string>());
            }

            return subStatuscode ?? Convert.ToInt32(tcpStats?.StoreResult?.SubStatusCode);
        }

        public KeyValuePair<string, object>[] PopulateInstanceCountDimensions(Uri accountEndpoint)
        {
            return new[]
            {
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbSystemName, OpenTelemetryCoreRecorder.CosmosDb),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerAddress, accountEndpoint.Host),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerPort, accountEndpoint.Port)
            };
        }
    }
}
