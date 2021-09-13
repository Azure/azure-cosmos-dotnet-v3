//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.DocDBErrors
{
    using System.Net;
    using Microsoft.Azure.Documents;

    internal static class ExceptionClassifier
    {
        public static DocDbError ClassifyStatusCodes(
            HttpStatusCode statusCode,
            int subStatusCode)
        {
            if (statusCode == HttpStatusCode.Gone && (subStatusCode == (int)SubStatusCodes.PartitionKeyRangeGone || subStatusCode == (int)SubStatusCodes.CompletingSplit))
            {
                return DocDbError.PartitionSplit;
            }

            return DocDbError.Undefined;
        }
    }
}