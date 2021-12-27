//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Payloads
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using Microsoft.Azure.Documents;

    internal class RequestPayload
    {
        public RequestPayload(string id,
                            CosmosDiagnostics cosmosDiagnostics,
                            HttpStatusCode statusCode,
                            long responseSizeInBytes,
                            string containerId,
                            string databaseId,
                            OperationType operationType,
                            ResourceType resourceType,
                            string consistencyLevel,
                            double requestCharge)
        {
            this.Id = id;
            this.cosmosDiagnostics = cosmosDiagnostics;
            this.statusCode = statusCode;       
            this.responseSizeInBytes = responseSizeInBytes;
            this.containerId = containerId;
            this.databaseId = databaseId;
            this.operationType = operationType;
            this.resourceType = resourceType;
            this.consistencyLevel = consistencyLevel;
            this.requestCharge = requestCharge;
        }

        public string Id { get; }

        public CosmosDiagnostics cosmosDiagnostics { get; }

        public HttpStatusCode statusCode { get; }

        public long responseSizeInBytes { get; }

        public string containerId { get; }

        public string databaseId { get; }

        public OperationType operationType { get; }

        public ResourceType resourceType { get; }

        public string consistencyLevel { get; }

        public double requestCharge { get; }
    }
}
