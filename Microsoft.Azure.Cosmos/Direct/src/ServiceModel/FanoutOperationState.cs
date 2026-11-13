//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    /// <summary> 
    /// Specifies the fanout operation status.
    /// </summary>
    internal enum FanoutOperationState
    {
        /// <summary>
        ///  Started a fanout operation.
        /// </summary>
        Started,

        /// <summary>
        /// Completed a fanout operation.
        /// </summary>
        Completed
    }
}
