// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents;

    internal static class DistributedTransactionConstants
    {
        public const string EndpointPath = "/operations/dtc";
        public const string AuthorizationResourceType = "databaseaccount";
        public const string IdempotencyTokenHeader = "x-ms-cosmos-idempotency-token";
        public const string OperationTypeHeader = "x-ms-cosmos-operation-type";
        public const string ResourceTypeHeader = "x-ms-cosmos-resource-type";

        public static bool IsDistributedTransactionRequest(OperationType operationType, ResourceType resourceType)
        {
            return operationType == OperationType.CommitDistributedTransaction 
                && resourceType == ResourceType.DistributedTransactionBatch;
        }
    }
}
