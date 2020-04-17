//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// ResponseMessage to promote the FeedRangeContinuation as Continuation.
    /// </summary>
    internal sealed class FeedRangeResponse
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
                responseMessage.Headers.ContinuationToken = null;
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

            responseMessage.Headers.ContinuationToken = null;

            return responseMessage;
        }
    }
}