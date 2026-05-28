// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents;

    internal static class DistributedTransactionConstants
    {
        // DTX envelope sub-status codes. The full status/body matrix and the inner-vs-outer retry ownership
        // is documented at the call site in ClientRetryPolicy.ShouldRetryDtxRequest.
        internal const int DtcCoordinatorRaceConflict = 5352; // 449 sub-status: coordinator ETag race
        internal const int DtcLedgerThrottled = 3200;          // 429 sub-status: ledger backpressure
        internal const int DtcLedgerFailure = 5411;            // 500 sub-status: ledger infra failure
        internal const int DtcAccountConfigFailure = 5412;     // 500 sub-status: account-config infra failure
        internal const int DtcDispatchFailure = 5413;          // 500 sub-status: dispatch infra failure

        internal static bool IsDistributedTransactionRequest(OperationType operationType, ResourceType resourceType)
        {
            return operationType == OperationType.CommitDistributedTransaction
                && resourceType == ResourceType.DistributedTransactionBatch;
        }

        internal static string GetCollectionFullName(string database, string container)
        {
            return $"dbs/{database}/colls/{container}";
        }
    }
}
