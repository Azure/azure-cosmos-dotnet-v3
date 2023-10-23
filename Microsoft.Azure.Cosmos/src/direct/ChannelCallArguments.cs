//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.FaultInjection; 

    internal sealed class ChannelCallArguments : IDisposable
    {
        private readonly ChannelCommonArguments commonArguments;
        private readonly OperationType operationType;
        private readonly ResourceType resourceType;
        private readonly string resolvedCollectionRid;
        private readonly INameValueCollection requestHeaders;

        public ChannelCallArguments (Guid activityId)
        {
            this.commonArguments = new ChannelCommonArguments(
                activityId, TransportErrorCode.RequestTimeout,
                userPayload: true);
        }

        public ChannelCallArguments(
            Guid activityId, 
            OperationType operationType, 
            ResourceType resourceType, 
            string resolvedCollectionRid, 
            INameValueCollection requestHeaders)
        {
            this.commonArguments = new ChannelCommonArguments(
                activityId, TransportErrorCode.RequestTimeout,
                userPayload: true);
            this.operationType = operationType;
            this.resourceType = resourceType;
            this.resolvedCollectionRid = resolvedCollectionRid;
            this.requestHeaders = requestHeaders;
        }

        public ChannelCommonArguments CommonArguments { get { return this.commonArguments; } }

        public Dispatcher.PrepareCallResult PreparedCall { get; set; }

        public OperationType OperationType { get { return this.operationType;  } }

        public ResourceType ResourceType { get { return this.resourceType; } }

        public string ResolvedCollectionRid { get { return this.resolvedCollectionRid; } }

        public INameValueCollection RequestHeaders { get { return this.requestHeaders; } }

        /// <inheritdoc />
        public void Dispose()
        {
            this.PreparedCall?.Dispose();
        }
    }
}