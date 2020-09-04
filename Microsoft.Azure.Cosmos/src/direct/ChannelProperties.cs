//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Diagnostics;

    internal sealed class ChannelProperties
    {
        public ChannelProperties(UserAgentContainer userAgent,
            string certificateHostNameOverride, IConnectionStateListener connectionStateListener,
            TimeSpan requestTimeout, TimeSpan openTimeout,
            PortReuseMode portReuseMode, UserPortPool userPortPool,
            int maxChannels, int partitionCount, int maxRequestsPerChannel,
            TimeSpan receiveHangDetectionTime, TimeSpan sendHangDetectionTime,
            TimeSpan idleTimeout,
            RntbdConstants.CallerId callerId)
        {
            Debug.Assert(userAgent != null);
            this.UserAgent = userAgent;
            this.CertificateHostNameOverride = certificateHostNameOverride;
            this.ConnectionStateListener = connectionStateListener;

            this.RequestTimeout = requestTimeout;
            this.OpenTimeout = openTimeout;

            this.PortReuseMode = portReuseMode;
            this.UserPortPool = userPortPool;

            Debug.Assert(maxChannels > 0);
            this.MaxChannels = maxChannels;
            Debug.Assert(partitionCount > 0);
            this.PartitionCount = partitionCount;
            Debug.Assert(maxRequestsPerChannel > 0);
            this.MaxRequestsPerChannel = maxRequestsPerChannel;

            Debug.Assert(receiveHangDetectionTime > TimeSpan.Zero);
            this.ReceiveHangDetectionTime = receiveHangDetectionTime;
            Debug.Assert(sendHangDetectionTime > TimeSpan.Zero);
            this.SendHangDetectionTime = sendHangDetectionTime;
            this.IdleTimeout = idleTimeout;
            this.CallerId = callerId;
        }

        public UserAgentContainer UserAgent { get; private set; }

        public string CertificateHostNameOverride { get; private set; }

        public IConnectionStateListener ConnectionStateListener { get; private set; }

        public TimeSpan RequestTimeout { get; private set; }

        public TimeSpan OpenTimeout { get; private set; }

        public PortReuseMode PortReuseMode { get; private set; }

        public int MaxChannels { get; private set; }

        public int PartitionCount { get; private set; }

        public int MaxRequestsPerChannel { get; private set; }

        public TimeSpan ReceiveHangDetectionTime { get; private set; }

        public TimeSpan SendHangDetectionTime { get; private set; }

        public TimeSpan IdleTimeout { get; private set; }

        public UserPortPool UserPortPool { get; private set; }

        public RntbdConstants.CallerId CallerId { get; private set; }
    }
}
