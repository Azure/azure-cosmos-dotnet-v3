// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    [Flags]
    internal enum CosmosClientOptionsFeatures
    {
        AllowBulkExecution = 0x00000000001
    }
}
