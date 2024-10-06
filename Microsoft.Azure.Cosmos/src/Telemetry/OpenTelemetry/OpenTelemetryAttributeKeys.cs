//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    /// <summary>
    /// Contains constant string values representing OpenTelemetry attribute keys for monitoring and tracing Cosmos DB operations.
    /// These keys follow the OpenTelemetry conventions and the Cosmos DB semantic conventions as outlined in the OpenTelemetry specification.
    /// </summary>
    /// <remarks>
    /// For more details on the semantic conventions, refer to the OpenTelemetry documentation at:
    /// <see href="https://opentelemetry.io/docs/specs/semconv/database/cosmosdb/"/>
    /// </remarks>
    internal sealed class OpenTelemetryAttributeKeys
    {
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
        public const string DbSystemName = "db.system";

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

        // Cosmos DB specific attributes

        /// <summary>
        /// Represents the client ID for Cosmos DB.
        /// </summary>
        public const string ClientId = "db.cosmosdb.client_id";

        /// <summary>
        /// Represents the machine ID for Cosmos DB.
        /// </summary>
        public const string MachineId = "db.cosmosdb.machine_id";

        /// <summary>
        /// Represents the user agent, compliant with OpenTelemetry conventions.
        /// </summary>
        public const string UserAgent = "user_agent.original";

        /// <summary>
        /// Represents the connection mode for Cosmos DB.
        /// </summary>
        public const string ConnectionMode = "db.cosmosdb.connection_mode";

        /// <summary>
        /// Represents the type of operation for Cosmos DB.
        /// </summary>
        public const string OperationType = "db.cosmosdb.operation_type";

        // Request/Response specifics

        /// <summary>
        /// Represents the name of the container in Cosmos DB.
        /// </summary>
        public const string ContainerName = "db.collection.name";

        /// <summary>
        /// Represents the content length of the request.
        /// </summary>
        public const string RequestContentLength = "db.cosmosdb.request_content_length";

        /// <summary>
        /// Represents the content length of the response.
        /// </summary>
        public const string ResponseContentLength = "db.cosmosdb.response_content_length";

        /// <summary>
        /// Represents the status code of the response.
        /// </summary>
        public const string StatusCode = "db.cosmosdb.status_code";

        /// <summary>
        /// Represents the sub-status code of the response.
        /// </summary>
        public const string SubStatusCode = "db.cosmosdb.sub_status_code";

        /// <summary>
        /// Represents the request charge for the operation.
        /// </summary>
        public const string RequestCharge = "db.cosmosdb.request_charge";

        /// <summary>
        /// Represents the regions contacted for the operation.
        /// </summary>
        public const string Region = "db.cosmosdb.regions_contacted";

        /// <summary>
        /// Represents the item count in the operation.
        /// </summary>
        public const string ItemCount = "db.cosmosdb.item_count";

        /// <summary>
        /// Represents the activity ID for the operation.
        /// </summary>
        public const string ActivityId = "db.cosmosdb.activity_id";

        /// <summary>
        /// Represents the correlated activity ID for the operation.
        /// </summary>
        public const string CorrelatedActivityId = "db.cosmosdb.correlated_activity_id";

        /// <summary>
        /// Represents the Azure Cosmos DB SQL Query.
        /// </summary>
        public const string QueryText = "db.query.text";

        /// <summary>
        /// Represents the size of the batch operation.
        /// </summary>
        public const string BatchSize = "db.operation.batch_size";

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
    }
}
