//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// HTTP headers in a <see cref="ResponseMessage"/>.
    /// </summary>
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public
#else
    internal
#endif
        class CosmosQueryResponseMessageHeaders : Headers
    {
        internal CosmosQueryResponseMessageHeaders(
            string continauationToken,
            string disallowContinuationTokenMessage,
            ResourceType resourceType,
            string containerRid)
        {
            base.ContinuationToken = continauationToken;
            this.DisallowContinuationTokenMessage = disallowContinuationTokenMessage;
            this.ResourceType = resourceType;
            this.ContainerRid = containerRid;
        }

        public string DisallowContinuationTokenMessage { get; }

        public override string ContinuationToken
        {
            get
            {
                if (this.DisallowContinuationTokenMessage != null)
                {
                    throw new ArgumentException(this.DisallowContinuationTokenMessage);
                }

                return base.ContinuationToken;
            }

            internal set
            {
                throw new InvalidOperationException("To prevent the different aggregate context from impacting each other only allow updating the continuation token via clone method.");
            }
        }

        public virtual string ContainerRid { get; }

        internal virtual ResourceType ResourceType { get; }

        public string InternalContinuationToken => base.ContinuationToken;

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
                this.ResourceType,
                this.ContainerRid)
            {
                RequestCharge = this.RequestCharge,
                ContentLength = this.ContentLength,
                ActivityId = this.ActivityId,
                ETag = this.ETag,
                Location = this.Location,
                RetryAfterLiteral = this.RetryAfterLiteral,
                SubStatusCodeLiteral = this.SubStatusCodeLiteral,
                ContentType = this.ContentType,
                QueryMetricsText = QueryMetricsText
            };
        }

        internal static CosmosQueryResponseMessageHeaders ConvertToQueryHeaders(
            Headers sourceHeaders,
            ResourceType resourceType,
            string containerRid)
        {
            if (sourceHeaders == null)
            {
                return new CosmosQueryResponseMessageHeaders(
                    continauationToken: null,
                    disallowContinuationTokenMessage: null,
                    resourceType: resourceType,
                    containerRid: containerRid);
            }

            return new CosmosQueryResponseMessageHeaders(
                continauationToken: sourceHeaders.ContinuationToken,
                disallowContinuationTokenMessage: null,
                resourceType: resourceType,
                containerRid: containerRid)
            {
                RequestCharge = sourceHeaders.RequestCharge,
                ContentLength = sourceHeaders.ContentLength,
                ActivityId = sourceHeaders.ActivityId,
                ETag = sourceHeaders.ETag,
                Location = sourceHeaders.Location,
                RetryAfterLiteral = sourceHeaders.RetryAfterLiteral,
                SubStatusCodeLiteral = sourceHeaders.SubStatusCodeLiteral,
                ContentType = sourceHeaders.ContentType,
                QueryMetricsText = sourceHeaders.QueryMetricsText
            };
        }
    }
}