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
    internal sealed class CosmosQueryResponseMessageHeaders : Headers
    {
        public CosmosQueryResponseMessageHeaders(
            double requestCharge,
            string activityId,
            SubStatusCodes subStatusCode,
            string continuationToken,
            string disallowContinuationTokenMessage,
            ResourceType resourceType,
            string containerRid,
            int itemCount)
             : base(
                requestCharge,
                activityId,
                subStatusCode,
                continuationToken,
                itemCount)
        {
            this.DisallowContinuationTokenMessage = disallowContinuationTokenMessage;
            this.ResourceType = resourceType;
            this.ContainerRid = containerRid;
        }

        internal string DisallowContinuationTokenMessage { get; }

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

            internal set => throw new InvalidOperationException("To prevent the different aggregate context from impacting each other only allow updating the continuation token via clone method.");
        }

        internal string ContainerRid { get; }

        internal ResourceType ResourceType { get; }

        internal string InternalContinuationToken => base.ContinuationToken;
    }
}