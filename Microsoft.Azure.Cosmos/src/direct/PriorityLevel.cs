//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
#if COSMOSCLIENT
    internal
#else
    public
#endif
    enum PriorityLevel
    {
        High = 1,
        Low = 2,
    }
}
