// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents;

    internal static class DistributedTransactionConstants
    {
        // Sub-status codes returned on the envelope response for distributed transactions.
        // Source: dtx-sdk-response-status-codes.md — Part A, Section 1.

        /// <summary>449/5352 — Coordinator race conflict (ETag contention on the ledger exhausted).</summary>
        internal const int DtcCoordinatorRaceConflict = 5352;

        /// <summary>429/3200 — Ledger RU throttled and coordinator exhausted its internal retry budget.</summary>
        internal const int DtcLedgerThrottled = 3200;

        /// <summary>500/5411 — Ledger infrastructure failure.</summary>
        internal const int DtcLedgerFailure = 5411;

        /// <summary>500/5412 — Account configuration failure.</summary>
        internal const int DtcAccountConfigFailure = 5412;

        /// <summary>500/5413 — Coordinator dispatch failure.</summary>
        internal const int DtcDispatchFailure = 5413;

        internal static bool IsDistributedTransactionRequest(OperationType operationType, ResourceType resourceType)
        {
            return operationType == OperationType.CommitDistributedTransaction 
                && resourceType == ResourceType.DistributedTransactionBatch;
        }
    }
}
