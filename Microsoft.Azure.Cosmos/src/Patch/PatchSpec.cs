//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Newtonsoft.Json;

    /// <summary>
    /// Details of Patch operation that is to be applied to the referred Cosmos item.
    /// </summary>
    internal readonly struct PatchSpec
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PatchSpec"/> struct.
        /// </summary>
        /// <param name="patchOperations"></param>
        /// <param name="requestOptions"></param>
        public PatchSpec(
            IReadOnlyList<PatchOperation> patchOperations,
            Either<PatchItemRequestOptions, TransactionalBatchPatchItemRequestOptions> requestOptions)
        {
            if (patchOperations == null)
            {
                throw new ArgumentOutOfRangeException("Patch Operations cannot be null.");
            }

            List<PatchOperation> patchOperationsClone = new List<PatchOperation>(patchOperations.Count);
            foreach (PatchOperation operation in patchOperations)
            {
                patchOperationsClone.Add(operation);
            }
            this.PatchOperations = (IReadOnlyList<PatchOperation>)patchOperationsClone;

            this.RequestOptions = requestOptions;
        }

        /// <summary>
        /// Details of Patch operation that is to be applied to the referred Cosmos item.
        /// </summary>
        public IReadOnlyList<PatchOperation> PatchOperations { get; }

        /// <summary>
        /// Cosmos item request options specific to patch.
        /// </summary>
        public Either<PatchItemRequestOptions, TransactionalBatchPatchItemRequestOptions> RequestOptions { get; }
    }
}
