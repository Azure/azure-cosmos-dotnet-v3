// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Used to future-proof token versions
    /// </summary>
    internal enum FeedTokenType
    {
        EPKRange = 0,
        PartitionKeyValue = 1,
        PartitionKeyRangeId = 2 // For backward compatibility
    }
}
