//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    /// <summary> 
    /// Specifies whether or not the resource is to be indexed.
    /// </summary>
    internal enum MigrateCollectionDirective
    {
        /// <summary>
        ///  Move to SSD
        /// </summary>
        Thaw,

        /// <summary>
        /// Move to HDD.
        /// </summary>
        Freeze
    }
}
