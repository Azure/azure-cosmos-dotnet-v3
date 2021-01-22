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
            string condition = null)
        {
            if (patchOperations == null)
            {
                throw new ArgumentNullException(nameof(patchOperations));
            }
            this.PatchOperations = patchOperations;
            this.Condition = condition;
        }

        /// <summary>
        /// Details of Patch operation that is to be applied to the referred Cosmos item.
        /// </summary>
        public IReadOnlyList<PatchOperation> PatchOperations { get; }

        //[JsonProperty(PropertyName = PatchConstants.PatchSpecAttributes.Condition)]
        /// <summary>
        /// creates a conditional SQL argument which is of format "FROM X where <CONDITION>"
        /// the condition has to be withing the scope of the document which is supposed to be patch in the particular request.
        /// If the condition is satisfied the patch transaction will take place otherwise it will be retured with precondition failed.
        /// </summary>
        public string Condition { get; }
    }
}
