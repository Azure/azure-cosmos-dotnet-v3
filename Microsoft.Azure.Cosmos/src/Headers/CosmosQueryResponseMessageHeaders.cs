//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// HTTP headers in a <see cref="CosmosResponseMessage"/>.
    /// </summary>
    internal class CosmosQueryResponseMessageHeaders : CosmosResponseMessageHeaders
    {
        public CosmosQueryResponseMessageHeaders(string continauationToken, string disallowContinuationTokenMessage)
        {
            base.Continuation = continauationToken;
            this.DisallowContinuationTokenMessage = disallowContinuationTokenMessage;
        }

        internal string DisallowContinuationTokenMessage { get; }

        public override string Continuation
        {
            get
            {
                if (this.DisallowContinuationTokenMessage != null)
                {
                    throw new ArgumentException(this.DisallowContinuationTokenMessage);
                }

                return base.Continuation;
            }

            internal set
            {
                throw new InvalidOperationException("To prevent the different aggregate context from impacting each other only allow updating the continuation token via clone method.");
            }
        }

        internal string InternalContinuationToken => base.Continuation;

        internal CosmosQueryResponseMessageHeaders CloneKnownProperties()
        {
            return this.CloneKnownProperties(
                this.InternalContinuationToken, 
                this.DisallowContinuationTokenMessage);
        }

        internal CosmosQueryResponseMessageHeaders CloneKnownProperties(
            string continauationToken, 
            string disallowContinuationTokenMessage)
        {
            return new CosmosQueryResponseMessageHeaders(continauationToken, disallowContinuationTokenMessage)
            {
                RequestCharge = this.RequestCharge,
                ContentLength = this.ContentLength,
                ActivityId = this.ActivityId,
                ETag = this.ETag,
                Location = this.Location,
                RetryAfterLiteral = this.RetryAfterLiteral,
                SubStatusCodeLiteral = this.SubStatusCodeLiteral,
                ContentType = this.ContentType,
            };
        }

        internal static CosmosQueryResponseMessageHeaders ConvertToQueryHeaders(
            CosmosResponseMessageHeaders sourceHeaders)
        {
            return new CosmosQueryResponseMessageHeaders(sourceHeaders.Continuation, null)
            {
                RequestCharge = sourceHeaders.RequestCharge,
                ContentLength = sourceHeaders.ContentLength,
                ActivityId = sourceHeaders.ActivityId,
                ETag = sourceHeaders.ETag,
                Location = sourceHeaders.Location,
                RetryAfterLiteral = sourceHeaders.RetryAfterLiteral,
                SubStatusCodeLiteral = sourceHeaders.SubStatusCodeLiteral,
                ContentType = sourceHeaders.ContentType,
            };
        }
    }
}