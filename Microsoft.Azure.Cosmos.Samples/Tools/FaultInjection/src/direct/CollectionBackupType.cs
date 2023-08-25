//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    /// <summary> 
    /// Specifies the supported collection backup type in the Azure Cosmos DB service.
    /// </summary>
    internal enum CollectionBackupType
    {
        /// <summary>
        /// CollectionBackupType is defiined with a create collection operation.
        /// </summary>
        /// <remarks>
        /// With Invalid CollectionBackupType, we will reject the logstore collection creation.
        /// 
        /// The default CollectionBackupType is Invalid.
        /// </remarks>
        Invalid,

        /// <summary>
        /// CollectionBackupType is defiined with a create collection operation.
        /// </summary>
        /// <remarks>
        /// With Continuous CollectionBackupType, we will allow LogStore collection creation if valid retention value is set.
        /// </remarks>
        Continuous
    }
}
