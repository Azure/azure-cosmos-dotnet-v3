//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    internal class CosmosInstrumentationConstants
    {
        public const string DiagnosticNamespace = "Azure.Cosmos";
        public const string ResourceProviderNamespace = "Microsoft.Azure.Cosmos";
        public const string OperationPrefix = "Cosmos";

/*        public const string DbSystemKey = "db.type";
        public const string AccountNameKey = "db.url";
        public const string DbNameKey = "db.instance";*/
        public const string DbOperationKey = "db.statement";

        public const string DbName = "db.name";
        public const string DbSystemName = "db.system";
        public const string NetPeerName = "net.peer.name";
        public const string NetPeerPort = "net.peer.port";

        public const string UserAgentKey = "User Agent";
        public const string ConnectionMode = "Connection Mode";
        public const string HttpStatusCodeKey = "http.status_code";
        public const string ContainerNameKey = "Container Name";
        public const string RequestChargeKey = "Request Charge (RUs)";
        public const string RequestDiagnosticsKey = "Request Diagnostics (JSON)";
        public const string SubStatusCode = "SubStatusCode";
        public const string PageSize = "Page Size";

        public const string RequestSize = "Request Size";
        public const string ResponseSize = "Response Size";

        public const string ErrorKey = "error";
        public const string ExceptionKey = "Exception StackTrace";
    }
}
