//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.DocDBErrors
{
    using System.Net;
    using Microsoft.Azure.Documents;

    internal static class ExceptionClassifier
    {
        public static DocDbError ClassifyStatusCodes(
            HttpStatusCode statusCode, 
            SubStatusCodes subStatusCode)
        {
            if (statusCode == HttpStatusCode.NotFound && subStatusCode != SubStatusCodes.ReadSessionNotAvailable)
            {
                return DocDbError.PartitionNotFound;
            }

            if (statusCode == HttpStatusCode.Gone && (subStatusCode == SubStatusCodes.PartitionKeyRangeGone || subStatusCode == SubStatusCodes.CompletingSplit))
            {
                return DocDbError.PartitionSplit;
            }

            if (statusCode == HttpStatusCode.NotModified || statusCode == (HttpStatusCode)StatusCodes.TooManyRequests || statusCode >= HttpStatusCode.InternalServerError)
            {
                return DocDbError.TransientError;
            }

            return DocDbError.Undefined;
        }
    }
}