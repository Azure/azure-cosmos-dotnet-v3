// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents;

    internal static class DistributedTransactionConstants
    {
        public static bool IsDistributedTransactionRequest(OperationType operationType, ResourceType resourceType)
        {
            return operationType == OperationType.CommitDistributedTransaction 
                && resourceType == ResourceType.DistributedTransactionBatch;
        }
    }
}
