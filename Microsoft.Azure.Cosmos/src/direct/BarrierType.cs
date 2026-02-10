//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// Defines the barrier request types.
    /// </summary>
    internal enum BarrierType
    {
        /// <summary>
        /// Represents No barrier needed.
        /// </summary>
        None = 0,

        /// <summary>
        /// Represents barrier for global strong consistency writes.
        /// </summary>
        GlobalStrongWrite = 1,

        /// <summary>
        /// Represents barrier for N-region synchronous commit writes.
        /// </summary>
        NRegionSynchronousCommit = 2
    }
}
