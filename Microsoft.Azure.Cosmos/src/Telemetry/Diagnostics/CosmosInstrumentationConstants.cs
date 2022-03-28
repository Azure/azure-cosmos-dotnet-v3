//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    internal class CosmosInstrumentationConstants
    {
        public const string DiagnosticNamespace = "Azure.Cosmos";
        public const string ResourceProviderNamespace = "Microsoft.DocumentDB";
        public const string OperationPrefix = "Cosmos";

        public const string DbSystemName = "db.system";
        public const string DbName = "db.name";
        public const string DbOperation = "db.operation";

        public const string Account = "db.cosmosdb.account";
        public const string ContainerName = "db.cosmosdb.container";
        public const string PartitionId = "db.cosmosdb.partitionId";
        public const string StatusCode = "db.cosmosdb.status_code";
        public const string UserAgent = "db.cosmosdb.user_agent";
        public const string RequestContentLength = "db.cosmosdb.request_content_length";
        public const string ResponseContentLength = "db.cosmosdb.response_content_length";
        public const string Region = "db.cosmosdb.region";
        public const string RetryCount = "db.cosmosdb.retry_count";
        public const string ConnectionMode = "db.cosmosdb.connection_mode";
        public const string BackendStatusCodes = "db.cosmosdb.backend_status_codes";
        public const string ItemCount = "db.cosmosdb.item_count";
        public const string RequestDiagnostics = "db.cosmosdb.request_diagnostics";
        public const string RequestCharge = "db.cosmosdb.request_charge";

        public const string ExceptionType = "exception.type";
        public const string ExceptionMessage = "exception.message";
        public const string ExceptionStacktrace = "exception.stacktrace";
    }
}
