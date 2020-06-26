// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    internal delegate PartitionRangePageEnumerator CreatePartitionRangePageEnumerator(FeedRange feedRange, State state);
}
