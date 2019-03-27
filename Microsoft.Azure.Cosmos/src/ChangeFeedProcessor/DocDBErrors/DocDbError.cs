//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.DocDBErrors
{
    internal enum DocDbError
    {
        Undefined,
        PartitionNotFound,
        PartitionSplit,
        TransientError,
        MaxItemCountTooLarge,
    }
}