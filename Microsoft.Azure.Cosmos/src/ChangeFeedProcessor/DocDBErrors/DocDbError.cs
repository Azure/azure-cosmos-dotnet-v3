//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.DocDBErrors
{
    internal enum DocDbError
    {
        Undefined,
        PartitionSplit,
        PartitionNotFound,
        ReadSessionNotAvailable
    }
}