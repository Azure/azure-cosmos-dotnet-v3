//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Common
{
    internal interface ISecondaryIndexMetadataInvalidationTarget
    {
        /// <summary>
        /// Invalidates all cached secondary index metadata for a source collection.
        /// </summary>
        void Invalidate(string sourceCollectionRid);
    }
}
