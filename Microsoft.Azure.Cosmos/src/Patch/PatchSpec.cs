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
#if INTERNAL
    public
#else
    internal
#endif

        class PatchSpec
    {
        public PatchSpec(
            IReadOnlyList<PatchOperation> patchOperations,
            PatchRequestOptions patchRequestOptions = null)
        {
            if (patchOperations == null)
            {
                throw new ArgumentNullException(nameof(patchOperations));
            }
            this.PatchOperations = patchOperations;
            this.PatchRequestOptions = patchRequestOptions;
        }

        /// <summary>
        /// Details of Patch operation that is to be applied to the referred Cosmos item.
        /// </summary>
        public IReadOnlyList<PatchOperation> PatchOperations { get; }

        /// <summary>
        /// Details of Patch operation that is to be applied to the referred Cosmos item.
        /// </summary>
        public PatchRequestOptions PatchRequestOptions { get; }
    }
}
