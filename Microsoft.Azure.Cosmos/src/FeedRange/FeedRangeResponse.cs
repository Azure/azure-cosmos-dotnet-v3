//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.IO;

    /// <summary>
    /// ResponseMessage to promote the FeedRangeContinuation as Continuation.
    /// </summary>
    internal sealed class FeedRangeResponse : ResponseMessage
    {
        private readonly ResponseMessage responseMessage;
        private readonly FeedRangeContinuation feedRangeContinuation;

        internal FeedRangeResponse(
            ResponseMessage responseMessage,
            FeedRangeContinuation feedRangeContinuation)
            : base(
                statusCode: responseMessage.StatusCode,
                requestMessage: responseMessage.RequestMessage,
                cosmosException: responseMessage.CosmosException,
                headers: responseMessage.Headers,
                diagnostics: responseMessage.DiagnosticsContext)
        {
            this.responseMessage = responseMessage;
            this.feedRangeContinuation = feedRangeContinuation;
        }

        public override Stream Content
        {
            get
            {
                return this.responseMessage.Content;
            }
        }

        public override string ContinuationToken
        {
            get
            {
                if (this.feedRangeContinuation.IsDone)
                {
                    return null;
                }

                return this.feedRangeContinuation.ToString();
            }
        }
    }
}