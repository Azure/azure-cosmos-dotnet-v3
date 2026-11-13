// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Indicates the response mode the coordinator applied when processing a distributed transaction.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    enum DistributedTransactionResponseMode
    {
        /// <summary>
        /// The coordinator returned the full, standard response payload for the transaction.
        /// </summary>
        Standard = 0,

        /// <summary>
        /// The coordinator returned a fast, minimal response for the transaction.
        /// </summary>
        FastResponse = 1,
    }
}
