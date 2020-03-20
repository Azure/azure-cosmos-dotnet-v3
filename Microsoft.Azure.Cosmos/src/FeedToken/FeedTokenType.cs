// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// used to help on deserialization
    /// </summary>
    internal enum FeedTokenType
    {
        EPKRange = 0,
        PartitionKeyValue = 1, // Used for PK-based Change Feed
        PartitionKeyRangeId = 2 // For backward compatibility
    }
}
