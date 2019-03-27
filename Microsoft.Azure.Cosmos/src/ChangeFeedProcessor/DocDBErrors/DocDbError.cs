//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
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