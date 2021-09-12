//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    internal abstract class TransactionalBatchInternal : TransactionalBatch
    {
#if !PREVIEW
        /// <summary>
        /// Adds an operation to patch an item into the batch.
        /// </summary>
        /// <param name="id">The unique id of the item.</param>
        /// <param name="patchOperations">Represents a list of operations to be sequentially applied to the referred Cosmos item.</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <returns>The transactional batch instance with the operation added.</returns>
        public abstract TransactionalBatch PatchItem(
                string id,
                System.Collections.Generic.IReadOnlyList<PatchOperation> patchOperations,
                TransactionalBatchPatchItemRequestOptions requestOptions = null);
#endif
    }
}
