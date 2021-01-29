//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// Details of Patch operation that is to be applied to the referred Cosmos item.
    /// </summary>
    internal struct PatchSpec
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PatchSpec"/> struct.
        /// </summary>
        /// <param name="patchOperations"></param>
        /// <param name="patchRequestOptions"></param>
        public PatchSpec(
            IReadOnlyList<PatchOperation> patchOperations,
            PatchRequestOptions patchRequestOptions = null)
        {
            this.PatchOperations = patchOperations ?? throw new ArgumentNullException(nameof(patchOperations));
            this.PatchRequestOptions = patchRequestOptions;
            this.BatchPatchRequestOptions = null;
        }

        public PatchSpec(
            IReadOnlyList<PatchOperation> patchOperations,
            TransactionalBatchPatchRequestOptions batchPatchRequestOptions = null)
        {
            this.PatchOperations = patchOperations ?? throw new ArgumentNullException(nameof(patchOperations));
            this.PatchRequestOptions = null;
            this.BatchPatchRequestOptions = batchPatchRequestOptions;
        }

        /// <summary>
        /// Details of Patch operation that is to be applied to the referred Cosmos item.
        /// </summary>
        public IReadOnlyList<PatchOperation> PatchOperations { get; }

        /// <summary>
        /// Cosmos item request options specific to patch.
        /// </summary>
        public PatchRequestOptions PatchRequestOptions { get; }

        /// <summary>
        /// Cosmos item request options specific to batch patch.
        /// </summary>
        public TransactionalBatchPatchRequestOptions BatchPatchRequestOptions { get; }
    }
}
