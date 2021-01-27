//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Diagnostics;

    internal sealed class ChannelOpenArguments
    {
        private readonly ChannelCommonArguments commonArguments;
        private readonly ChannelOpenTimeline openTimeline;
        private readonly TimeSpan openTimeout;
        private readonly PortReuseMode portReuseMode;
        private readonly UserPortPool userPortPool;
        private readonly RntbdConstants.CallerId callerId;

        public ChannelOpenArguments(
            Guid activityId,
            ChannelOpenTimeline openTimeline,
            TimeSpan openTimeout,
            PortReuseMode portReuseMode,
            UserPortPool userPortPool,
            RntbdConstants.CallerId callerId)
        {
            this.commonArguments = new ChannelCommonArguments(
                activityId, TransportErrorCode.ChannelOpenTimeout,
                userPayload: false);
            this.openTimeline = openTimeline;
            this.openTimeout = openTimeout;
#if DEBUG
            switch (portReuseMode)
            {
                case PortReuseMode.ReuseUnicastPort:
                    Debug.Assert(userPortPool == null);
                    break;

                case PortReuseMode.PrivatePortPool:
                    Debug.Assert(userPortPool != null);
                    break;

                default:
                    Debug.Assert(
                        false,
                        string.Format(
                            "Unhandled enum value {0}. Please decide what are the " +
                            "class invariants for this value.",
                            portReuseMode.ToString()));
                    break;
            }
#endif
            this.portReuseMode = portReuseMode;
            this.userPortPool = userPortPool;
            this.callerId = callerId;
        }

        public ChannelCommonArguments CommonArguments { get { return this.commonArguments; } }

        public ChannelOpenTimeline OpenTimeline { get { return this.openTimeline; } }

        public TimeSpan OpenTimeout { get { return this.openTimeout; } }

        public PortReuseMode PortReuseMode { get { return this.portReuseMode; } }

        public UserPortPool PortPool { get { return this.userPortPool; } }

        public RntbdConstants.CallerId CallerId { get { return this.callerId; } }
    }
}
