//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.FaultInjection;
    using System;

    internal sealed class ChannelCallArguments : IDisposable
    {
        private readonly ChannelCommonArguments commonArguments;

        public ChannelCallArguments (Guid activityId)
        {
            this.commonArguments = new ChannelCommonArguments(
                activityId, TransportErrorCode.RequestTimeout,
                userPayload: true);
        }

        public ChannelCommonArguments CommonArguments { get { return this.commonArguments; } }

        public Dispatcher.PrepareCallResult PreparedCall { get; set; }

        public FaultInjectionRequestContext FaultInjectionRequestContext { get; set; }

        public OperationType OperationType { get; set; }

        public ResourceType ResourceType { get; set; }

        public string ResolvedCollectionRid { get; set; }

        public INameValueCollection RequestHeaders { get; set; }

        public int RequestTimeoutTimeInSeconds { get; set; }

        /// <inheritdoc />
        public void Dispose()
        {
            this.PreparedCall?.Dispose();
        }
    }
}