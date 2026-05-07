// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents;

    internal static class DistributedTransactionConstants
    {
        // Sub-status codes for distributed transaction (DTX) envelope responses.
        //
        // | Status   | Sub-status                           | Body   | Meaning                                              |
        // |----------|--------------------------------------|--------|------------------------------------------------------|
        // | 200      | —                                    | per-op | Committed                                            |
        // | 452      | —                                    | per-op | Aborted; `isRetriable` flag governs outer-loop retry |
        // | 408      | —                                    | empty  | Coordinator stuck / retries exhausted                |
        // | 449      | DtcCoordinatorRaceConflict (5352)    | empty  | Race conflict; coordinator ETag contention exhausted |
        // | 400      | 5405–5410                            | empty  | Validation failure (non-retriable)                   |
        // | 429      | DtcLedgerThrottled (3200)            | empty  | Ledger throttled; coordinator retries exhausted      |
        // | 500      | DtcLedgerFailure/AccountConfig/      | empty  | Infrastructure failure                               |
        // |          | Dispatch (5411–5413)                 |        |                                                      |
        //
        // Empty-body codes are retried by the inner loop (ClientRetryPolicy).
        // Per-op body codes (200, 452) are owned by the outer loop (DistributedTransactionCommitter).

        internal const int DtcCoordinatorRaceConflict = 5352;
        internal const int DtcLedgerThrottled = 3200;
        internal const int DtcLedgerFailure = 5411;
        internal const int DtcAccountConfigFailure = 5412;
        internal const int DtcDispatchFailure = 5413;

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
