//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary> 
    /// Priority Level of Request
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    enum PriorityLevel
    {
        /// <summary> 
        /// High Priority
        /// </summary>
        High = 1,

        /// <summary> 
        /// Low Priority
        /// </summary>
        Low = 2,
    }
}
