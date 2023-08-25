// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    [Flags]
    internal enum CosmosClientOptionsFeatures
    {
        NoFeatures = 0,
        AllowBulkExecution = 1,
        HttpClientFactory = 2
    }
}
