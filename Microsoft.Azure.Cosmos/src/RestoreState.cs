//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    /// <summary> 
    /// Specifies the different states of restore,
    /// should match RestoreConstants.h
    /// </summary>
    internal enum RestoreState
    {
        /// <summary>
        ///  Not Specified.
        /// </summary>
        Invalid,

        /// <summary>
        ///  Collection Restore is currently ongoing.
        /// </summary>
        RestorePending,

        /// <summary>
        ///  Collection Restore completed with success.
        /// </summary>
        RestoreCompleted,

        /// <summary>
        /// Collection Restore completed with failure.
        /// </summary>
        RestoreFailed
    }
}
