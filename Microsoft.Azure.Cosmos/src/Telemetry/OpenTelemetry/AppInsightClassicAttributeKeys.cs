//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    internal sealed class AppInsightClassicAttributeKeys
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
    }
}
