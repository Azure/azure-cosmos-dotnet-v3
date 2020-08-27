//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.Client;

    internal sealed class StoreClientFactory : IStoreClientFactory
    {
        private bool isDisposed = false;
        private readonly Protocol protocol;
        private readonly RetryWithConfiguration retryWithConfiguration;
        private TransportClient transportClient;
        private TransportClient fallbackClient = null;
        private ConnectionStateListener connectionStateListener = null;

        public StoreClientFactory(
            Protocol protocol,
            int requestTimeoutInSeconds,
            int maxConcurrentConnectionOpenRequests,
            UserAgentContainer userAgent = null, // optional for both HTTPS and RNTBD
            ICommunicationEventSource eventSource = null, // required for HTTPS, not used for RNTBD
            string overrideHostNameInCertificate = null, // optional for RNTBD, not used for HTTPS
            int openTimeoutInSeconds = 0, // optional for RNTBD, not used for HTTPS
            int idleTimeoutInSeconds = -1,// optional for both HTTPS and RNTBD
            int timerPoolGranularityInSeconds = 0, // optional for RNTBD, not used for HTTPS
            int maxRntbdChannels = ushort.MaxValue,  // RNTBD
            int rntbdPartitionCount = 1, // RNTBD
            int maxRequestsPerRntbdChannel = 30,  // RNTBD
            PortReuseMode rntbdPortReuseMode = PortReuseMode.ReuseUnicastPort,  // RNTBD
            int rntbdPortPoolReuseThreshold = 256, // RNTBD
            int rntbdPortPoolBindAttempts = 5,  // RNTBD
            int receiveHangDetectionTimeSeconds = 65,  // RNTBD
            int sendHangDetectionTimeSeconds = 10,  // RNTBD
            bool enableCpuMonitor = true,
            RetryWithConfiguration retryWithConfiguration = null,
            RntbdConstants.CallerId callerId = RntbdConstants.CallerId.Anonymous, // replicatedResourceClient
            bool enableTcpConnectionEndpointRediscovery = false,
            IAddressResolver addressResolver = null) // globalAddressResolver
        {
            // <=0 means idle timeout is disabled.
            // valid value: >= 10 minutes
            if (idleTimeoutInSeconds > 0 && idleTimeoutInSeconds < 600)
            {
                throw new ArgumentOutOfRangeException(nameof(idleTimeoutInSeconds));
            }

            if (protocol == Protocol.Https)
            {
                if (eventSource == null)
                {
                    throw new ArgumentOutOfRangeException("eventSource");
                }

                this.transportClient = new HttpTransportClient(requestTimeoutInSeconds, eventSource, userAgent, idleTimeoutInSeconds);
            }
            else if (protocol == Protocol.Tcp)
            {
                if (maxRntbdChannels <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxRntbdChannels));
                }

                if ((rntbdPartitionCount < 1) || (rntbdPartitionCount > 8))
                {
                    throw new ArgumentOutOfRangeException(nameof(rntbdPartitionCount));
                }

                if (maxRequestsPerRntbdChannel <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxRequestsPerRntbdChannel));
                }

                if (maxRntbdChannels > ushort.MaxValue)
                {
                    DefaultTrace.TraceWarning(
                        "The value of {0} is unreasonably large. Received: {1}. " +
                        "Use {2} to represent \"effectively infinite\".",
                        nameof(maxRntbdChannels),
                        maxRntbdChannels,
                        ushort.MaxValue);
                }

                const int minRecommendedMaxRequestsPerChannel = 6;
                const int maxRecommendedMaxRequestsPerChannel = 256;
                if (maxRequestsPerRntbdChannel < minRecommendedMaxRequestsPerChannel)
                {
                    DefaultTrace.TraceWarning(
                        "The value of {0} is unreasonably small. Received: {1}. " +
                        "Small values of {0} can cause a large number of RNTBD " +
                        "channels to be opened to the same back-end. Reasonable " +
                        "values are between {2} and {3}",
                        nameof(maxRequestsPerRntbdChannel),
                        maxRequestsPerRntbdChannel,
                        minRecommendedMaxRequestsPerChannel,
                        maxRecommendedMaxRequestsPerChannel);
                }

                if (maxRequestsPerRntbdChannel > maxRecommendedMaxRequestsPerChannel)
                {
                    DefaultTrace.TraceWarning(
                        "The value of {0} is unreasonably large. Received: {1}. " +
                        "Large values of {0} can cause significant head-of-line " +
                        "blocking over RNTBD channels. Reasonable values are between {2} and {3}",
                        nameof(maxRequestsPerRntbdChannel),
                        maxRequestsPerRntbdChannel,
                        minRecommendedMaxRequestsPerChannel,
                        maxRecommendedMaxRequestsPerChannel);
                }

                // Not related to maxRecommendedMaxRequestsPerChannel.
                const int minRequiredSimultaneousRequests = 512;
                if (checked(maxRntbdChannels * maxRequestsPerRntbdChannel) <
                    minRequiredSimultaneousRequests)
                {
                    DefaultTrace.TraceWarning(
                        "The number of simultaneous requests allowed per backend " +
                        "is unreasonably small. Received {0} = {1}, {2} = {3}. " +
                        "Reasonable values are at least {4}",
                        nameof(maxRntbdChannels), maxRntbdChannels,
                        nameof(maxRequestsPerRntbdChannel), maxRequestsPerRntbdChannel,
                        minRequiredSimultaneousRequests);
                }

                StoreClientFactory.ValidatePortPoolReuseThreshold(ref rntbdPortPoolReuseThreshold);
                StoreClientFactory.ValidatePortPoolBindAttempts(ref rntbdPortPoolBindAttempts);
                if (rntbdPortPoolBindAttempts > rntbdPortPoolReuseThreshold)
                {
                    DefaultTrace.TraceWarning(
                        "Raising the value of {0} from {1} to {2} to match the value of {3}",
                        nameof(rntbdPortPoolReuseThreshold), rntbdPortPoolReuseThreshold,
                        rntbdPortPoolBindAttempts + 1, nameof(rntbdPortPoolBindAttempts));
                    rntbdPortPoolReuseThreshold = rntbdPortPoolBindAttempts;
                }

                const int minReceiveHangDetectionTimeSeconds = 65;
                const int maxReceiveHangDetectionTimeSeconds = 180;
                if (receiveHangDetectionTimeSeconds < minReceiveHangDetectionTimeSeconds)
                {
                    DefaultTrace.TraceWarning(
                        "The value of {0} is too small. Received {1}. Adjusting to {2}",
                        nameof(receiveHangDetectionTimeSeconds),
                        receiveHangDetectionTimeSeconds,
                        minReceiveHangDetectionTimeSeconds);
                    receiveHangDetectionTimeSeconds = minReceiveHangDetectionTimeSeconds;
                }

                if (receiveHangDetectionTimeSeconds > maxReceiveHangDetectionTimeSeconds)
                {
                    DefaultTrace.TraceWarning(
                        "The value of {0} is too large. Received {1}. Adjusting to {2}",
                        nameof(receiveHangDetectionTimeSeconds),
                        receiveHangDetectionTimeSeconds,
                        maxReceiveHangDetectionTimeSeconds);
                    receiveHangDetectionTimeSeconds = maxReceiveHangDetectionTimeSeconds;
                }

                const int minSendHangDetectionTimeSeconds = 2;
                const int maxSendHangDetectionTimeSeconds = 60;
                if (sendHangDetectionTimeSeconds < minSendHangDetectionTimeSeconds)
                {
                    DefaultTrace.TraceWarning(
                        "The value of {0} is too small. Received {1}. Adjusting to {2}",
                        nameof(sendHangDetectionTimeSeconds),
                        sendHangDetectionTimeSeconds,
                        minSendHangDetectionTimeSeconds);
                    sendHangDetectionTimeSeconds = minSendHangDetectionTimeSeconds;
                }

                if (sendHangDetectionTimeSeconds > maxSendHangDetectionTimeSeconds)
                {
                    DefaultTrace.TraceWarning(
                        "The value of {0} is too large. Received {1}. Adjusting to {2}",
                        nameof(sendHangDetectionTimeSeconds),
                        sendHangDetectionTimeSeconds,
                        maxSendHangDetectionTimeSeconds);
                    sendHangDetectionTimeSeconds = maxSendHangDetectionTimeSeconds;
                }

                if (enableTcpConnectionEndpointRediscovery && addressResolver != null)
                {
                    this.connectionStateListener = new ConnectionStateListener(addressResolver);
                }

                this.fallbackClient = new RntbdTransportClient(
                    requestTimeoutInSeconds,
                    maxConcurrentConnectionOpenRequests,
                    userAgent,
                    overrideHostNameInCertificate,
                    openTimeoutInSeconds,
                    idleTimeoutInSeconds,
                    timerPoolGranularityInSeconds);

                this.transportClient = new Rntbd.TransportClient(
                    new Rntbd.TransportClient.Options(TimeSpan.FromSeconds(requestTimeoutInSeconds))
                    {
                        MaxChannels = maxRntbdChannels,
                        PartitionCount = rntbdPartitionCount,
                        MaxRequestsPerChannel = maxRequestsPerRntbdChannel,
                        PortReuseMode = rntbdPortReuseMode,
                        PortPoolReuseThreshold = rntbdPortPoolReuseThreshold,
                        PortPoolBindAttempts = rntbdPortPoolBindAttempts,
                        ReceiveHangDetectionTime = TimeSpan.FromSeconds(receiveHangDetectionTimeSeconds),
                        SendHangDetectionTime = TimeSpan.FromSeconds(sendHangDetectionTimeSeconds),
                        UserAgent = userAgent,
                        CertificateHostNameOverride = overrideHostNameInCertificate,
                        OpenTimeout = TimeSpan.FromSeconds(openTimeoutInSeconds),
                        TimerPoolResolution = TimeSpan.FromSeconds(timerPoolGranularityInSeconds),
                        IdleTimeout = TimeSpan.FromSeconds(idleTimeoutInSeconds),
                        EnableCpuMonitor = enableCpuMonitor,
                        CallerId = callerId,
                        ConnectionStateListener = this.connectionStateListener
                    });
            }
            else
            {
                throw new ArgumentOutOfRangeException("protocol", protocol, "Invalid protocol value");
            }

            this.protocol = protocol;
            this.retryWithConfiguration = retryWithConfiguration;
        }

        private StoreClientFactory(
            Protocol protocol,
            RetryWithConfiguration retryWithConfiguration,
            TransportClient transportClient,
            TransportClient fallbackClient,
            ConnectionStateListener connectionStateListener)
        {
            this.protocol = protocol;
            this.retryWithConfiguration = retryWithConfiguration;
            this.transportClient = transportClient;
            this.fallbackClient = fallbackClient;
            this.connectionStateListener = connectionStateListener;
        }

        internal StoreClientFactory Clone()
        {
            return new StoreClientFactory(
                this.protocol,
                this.retryWithConfiguration,
                this.transportClient,
                this.fallbackClient,
                this.connectionStateListener);
        }

        // Interceptor factory (used in V3 SDK by compute to intercept transport interactions for chargeback) 
        internal void WithTransportInterceptor(Func<TransportClient, TransportClient> transportClientHandlerFactory)
        {
            if (transportClientHandlerFactory == null)
            {
                throw new ArgumentNullException(nameof(transportClientHandlerFactory));
            }

            this.transportClient = transportClientHandlerFactory(this.transportClient);
            this.fallbackClient = transportClientHandlerFactory(this.fallbackClient);
        }

        public StoreClient CreateStoreClient(
            IAddressResolver addressResolver,
            ISessionContainer sessionContainer,
            IServiceConfigurationReader serviceConfigurationReader,
            IAuthorizationTokenProvider authorizationTokenProvider,
            bool enableRequestDiagnostics = false,
            bool enableReadRequestsFallback = false,
            bool useFallbackClient = true,
            bool useMultipleWriteLocations = false,
            bool detectClientConnectivityIssues = false)
        {
            this.ThrowIfDisposed();
            if (useFallbackClient && this.fallbackClient != null)
            {
                return new StoreClient(
                    addressResolver,
                    sessionContainer,
                    serviceConfigurationReader,
                    authorizationTokenProvider,
                    this.protocol,
                    // Use the fallback client instead of the default one.
                    this.fallbackClient,
                    enableRequestDiagnostics,
                    enableReadRequestsFallback,
                    useMultipleWriteLocations,
                    detectClientConnectivityIssues,
                    this.retryWithConfiguration);
            }

            return new StoreClient(
                addressResolver,
                sessionContainer,
                serviceConfigurationReader,
                authorizationTokenProvider,
                this.protocol,
                this.transportClient,
                enableRequestDiagnostics,
                enableReadRequestsFallback,
                useMultipleWriteLocations,
                detectClientConnectivityIssues,
                this.retryWithConfiguration);
        }

        #region IDisposable
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            if (this.transportClient != null)
            {
                this.transportClient.Dispose();
                this.transportClient = null;
            }

            if (this.fallbackClient != null)
            {
                this.fallbackClient.Dispose();
                this.fallbackClient = null;
            }

            this.isDisposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException("StoreClientFactory");
            }
        }
        #endregion

        private static void ValidatePortPoolReuseThreshold(ref int rntbdPortPoolReuseThreshold)
        {
            const int minRntbdPortPoolReuseThreshold = 32;
            const int maxRntbdPortPoolReuseThreshold = 2048;
            Debug.Assert(minRntbdPortPoolReuseThreshold < maxRntbdPortPoolReuseThreshold);
            if (rntbdPortPoolReuseThreshold < minRntbdPortPoolReuseThreshold)
            {
                DefaultTrace.TraceWarning(
                    "The value of {0} is too small. Received {1}. Adjusting to {2}",
                    nameof(rntbdPortPoolReuseThreshold),
                    rntbdPortPoolReuseThreshold, minRntbdPortPoolReuseThreshold);
                rntbdPortPoolReuseThreshold = minRntbdPortPoolReuseThreshold;
            }
            else if (rntbdPortPoolReuseThreshold > maxRntbdPortPoolReuseThreshold)
            {
                DefaultTrace.TraceWarning(
                    "The value of {0} is too large. Received {1}. Adjusting to {2}",
                    nameof(rntbdPortPoolReuseThreshold),
                    rntbdPortPoolReuseThreshold, maxRntbdPortPoolReuseThreshold);
                rntbdPortPoolReuseThreshold = maxRntbdPortPoolReuseThreshold;
            }
        }

        private static void ValidatePortPoolBindAttempts(ref int rntbdPortPoolBindAttempts)
        {
            const int minRntbdPortPoolBindAttempts = 3;
            const int maxRntbdPortPoolBindAttempts = 32;
            Debug.Assert(minRntbdPortPoolBindAttempts < maxRntbdPortPoolBindAttempts);
            if (rntbdPortPoolBindAttempts < minRntbdPortPoolBindAttempts)
            {
                DefaultTrace.TraceWarning(
                    "The value of {0} is too small. Received {1}. Adjusting to {2}",
                    nameof(rntbdPortPoolBindAttempts),
                    rntbdPortPoolBindAttempts, minRntbdPortPoolBindAttempts);
                rntbdPortPoolBindAttempts = minRntbdPortPoolBindAttempts;
            }
            else if (rntbdPortPoolBindAttempts > maxRntbdPortPoolBindAttempts)
            {
                DefaultTrace.TraceWarning(
                    "The value of {0} is too large. Received {1}. Adjusting to {2}",
                    nameof(rntbdPortPoolBindAttempts),
                    rntbdPortPoolBindAttempts, maxRntbdPortPoolBindAttempts);
                rntbdPortPoolBindAttempts = maxRntbdPortPoolBindAttempts;
            }
        }
    }
}
