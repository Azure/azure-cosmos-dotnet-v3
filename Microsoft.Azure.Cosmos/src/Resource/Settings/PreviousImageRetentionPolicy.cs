//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Specifies the previous image retention policy for a container in the Azure Cosmos DB service.
    /// This policy controls whether the previous image of a document is retained during change feed operations.
    /// </summary>
    [Flags]
#if PREVIEW
    public
#else
    internal
#endif
    enum PreviousImageRetentionPolicy
    {
        /// <summary>
        /// Previous image retention is disabled.
        /// </summary>
        Disabled = 0x00,

        /// <summary>
        /// Previous image retention is enabled for replace operations.
        /// </summary>
        EnabledForReplaceOperation = 0x01,

        /// <summary>
        /// Previous image retention is enabled for delete operations.
        /// </summary>
        EnabledForDeleteOperation = 0x02,

        /// <summary>
        /// Previous image retention is enabled for all operations (replace and delete).
        /// </summary>
        EnabledForAllOperations = EnabledForReplaceOperation | EnabledForDeleteOperation
    }
}
