//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Outcome of mapping a partition key to a physical partition key range, independent of any
    /// <see cref="DocumentServiceRequest"/>. Callers translate the outcome into their own contract
    /// (e.g. the gateway throws / returns null; distributed transactions apply no session token).
    /// </summary>
    internal enum PartitionKeyRangeResolutionKind
    {
        /// <summary>
        /// The key maps to exactly one range, returned via the out parameter.
        /// </summary>
        Resolved,

        /// <summary>
        /// The key could not be mapped because the currently cached routing/partition-key definition
        /// may be stale — unlike <see cref="KeyMismatch"/>, this is not a definite mismatch. Each caller
        /// picks its own reaction (the gateway refreshes its caches and retries — historically returning
        /// null; distributed transactions apply no session token and degrade to eventual consistency).
        /// </summary>
        StaleMetadata,

        /// <summary>
        /// The supplied key has a different number of components than the (up-to-date) partition-key
        /// definition, so it is a genuine mismatch (historically the gateway threw a 400 PartitionKeyMismatch).
        /// </summary>
        KeyMismatch,
    }
}
