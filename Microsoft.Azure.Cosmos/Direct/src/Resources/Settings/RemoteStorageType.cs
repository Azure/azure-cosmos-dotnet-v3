//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    /// <summary> 
    /// Specifies type of storage used
    /// </summary>
    internal enum RemoteStorageType
    {
        /// <summary>
        ///  Use Standard Storage
        /// </summary>
        NotSpecified,

        /// <summary>
        ///  Use Standard Storage
        /// </summary>
        Standard,

        /// <summary>
        /// Use Premium Storage
        /// </summary>
        Premium
    }
}
