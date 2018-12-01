//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Admin
{
    internal enum ModuleEvent
    {
        None = 1,
        Throttle = 2,
        Shutdown = 3,
        Fault = 4
    };        
}
