//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// HTTP headers in a <see cref="ResponseMessage"/>.
    /// </summary>
    internal class CosmosQueryResponseMessageHeaders : Headers
    {
        public CosmosQueryResponseMessageHeaders(
            double requestCharge,
            string activityId,
            SubStatusCodes subStatusCode,
            string continauationToken,
            string disallowContinuationTokenMessage,
            ResourceType resourceType,
            string containerRid,
            int itemCount)
        {
            this.RequestCharge = requestCharge;
            this.ActivityId = activityId;
            this.SubStatusCode = subStatusCode;
            base.ContinuationToken = continauationToken;
            this.DisallowContinuationTokenMessage = disallowContinuationTokenMessage;
            this.ResourceType = resourceType;
            this.ContainerRid = containerRid;
            this.Add(HttpConstants.HttpHeaders.ItemCount, itemCount.ToString(CultureInfo.InvariantCulture));
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

            internal set
            {
                throw new InvalidOperationException("To prevent the different aggregate context from impacting each other only allow updating the continuation token via clone method.");
            }
        }

        internal virtual string ContainerRid { get; }

        internal virtual ResourceType ResourceType { get; }

        internal string InternalContinuationToken => base.ContinuationToken;
    }
}