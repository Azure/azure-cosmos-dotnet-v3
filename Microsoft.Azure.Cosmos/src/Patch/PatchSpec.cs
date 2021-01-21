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
            this.PatchOperations = patchOperations;
            this.Condition = condition;
        }

        public IReadOnlyList<PatchOperation> PatchOperations { get; set; }

        //[JsonProperty(PropertyName = PatchConstants.PatchSpecAttributes.Condition)]
        public string Condition { get; }
    }
}
