//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Diagnostics;

    internal sealed class ChannelCallArguments
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
    }
}