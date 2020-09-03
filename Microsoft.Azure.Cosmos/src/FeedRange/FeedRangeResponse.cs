//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// ResponseMessage to promote the FeedRangeContinuation as Continuation.
    /// </summary>
    internal static class FeedRangeResponse
    {
        public static ResponseMessage CreateSuccess(
            ResponseMessage responseMessage,
            FeedRangeContinuation feedRangeContinuation)
        {
            if (responseMessage == null)
            {
                throw new ArgumentNullException(nameof(responseMessage));
            }

            if (feedRangeContinuation == null)
            {
                throw new ArgumentNullException(nameof(feedRangeContinuation));
            }

            if (feedRangeContinuation.IsDone)
            {
                if (responseMessage.Headers.ContinuationToken != null)
                {
                    responseMessage.Headers.Remove(Documents.HttpConstants.HttpHeaders.Continuation);
                }
            }
            else
            {
                responseMessage.Headers.ContinuationToken = feedRangeContinuation.ToString();
            }

            return responseMessage;
        }

        public static ResponseMessage CreateFailure(ResponseMessage responseMessage)
        {
            if (responseMessage == null)
            {
                throw new ArgumentNullException(nameof(responseMessage));
            }

            if (responseMessage.Headers.ContinuationToken != null)
            {
                responseMessage.Headers.Remove(Documents.HttpConstants.HttpHeaders.Continuation);
            }

            return responseMessage;
        }
    }
}