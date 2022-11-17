//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
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

        /// <inheritdoc />
        public void Dispose()
        {
            this.PreparedCall?.Dispose();
        }
    }
}