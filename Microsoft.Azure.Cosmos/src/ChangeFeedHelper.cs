//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents;

    internal static class ChangeFeedHelper
    {
        internal static bool IsChangeFeedWithQueryRequest(
           OperationType operationType,
           bool hasPayload)
        {
            // ChangeFeed with payload is a CF with query support and will
            // be a query POST request.
            return operationType == OperationType.ReadFeed && hasPayload;
        }

    }
}
