//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    internal sealed class OpenTelemetryAttributeKeys
    {
        // Azure defaults
        public const string DiagnosticNamespace = "Azure.Cosmos";
        public const string ResourceProviderNamespace = "Microsoft.DocumentDB";
        public const string OperationPrefix = "Cosmos";

        // Common database attributes
        public const string DbSystemName = "db.system";
        public const string DbName = "db.name";
        public const string DbOperation = "db.operation";
        public const string NetPeerName = "net.peer.name";

        // Cosmos Db Specific
        public const string ClientId = "db.cosmosdb.client_id";
        public const string MachineId = "db.cosmosdb.machine_id";
        public const string UserAgent = "db.cosmosdb.user_agent";
        public const string ConnectionMode = "db.cosmosdb.connection_mode";

        // Request Specifics
        public const string ContainerName = "db.cosmosdb.container";
        public const string RequestContentLength = "db.cosmosdb.request_content_length_bytes";
        public const string ResponseContentLength = "db.cosmosdb.response_content_length_bytes";
        public const string StatusCode = "db.cosmosdb.status_code";
        public const string RequestCharge = "db.cosmosdb.request_charge";
        public const string Region = "db.cosmosdb.regions_contacted";
        public const string RetryCount = "db.cosmosdb.retry_count";
        public const string ItemCount = "db.cosmosdb.item_count";
        public const string RequestDiagnostics = "db.cosmosdb.request_diagnostics";

        // Exceptions
        public const string ExceptionType = "exception.type";
        public const string ExceptionMessage = "exception.message";
        public const string ExceptionStacktrace = "exception.stacktrace";
    }
}
