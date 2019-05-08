//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// HTTP headers in a <see cref="CosmosResponseMessage"/>.
    /// </summary>
    internal class CosmosQueryResponseMessageHeaders : CosmosResponseMessageHeaders
    {
        public CosmosQueryResponseMessageHeaders(
            string continauationToken, 
            string disallowContinuationTokenMessage, 
            ResourceType resourceType,
            string containerRid)
        {
            base.Continuation = continauationToken;
            this.DisallowContinuationTokenMessage = disallowContinuationTokenMessage;
            this.ResourceType = resourceType;
            this.ContainerRid = containerRid;
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

        internal virtual string ContainerRid { get; }

        internal virtual ResourceType ResourceType { get; }

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
            return new CosmosQueryResponseMessageHeaders(
                continauationToken,
                disallowContinuationTokenMessage, 
                this.ResourceType, this.ContainerRid)
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
            CosmosResponseMessageHeaders sourceHeaders,
            ResourceType resourceType,
            string containerRid)
        {
            return new CosmosQueryResponseMessageHeaders(
                sourceHeaders.Continuation, 
                null, 
                resourceType, 
                containerRid)
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