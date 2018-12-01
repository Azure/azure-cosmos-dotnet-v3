//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.Internal;

    internal sealed class StoreClientFactory : IDisposable
    {
        private bool isDisposed = false;
        private readonly Protocol protocol;
        private TransportClient transportClient;
        private TransportClient fallbackClient = null;

        public StoreClientFactory(
            Protocol protocol,
            int requestTimeoutInSeconds,
            int maxConcurrentConnectionOpenRequests,
            UserAgentContainer userAgent = null, // optional for both HTTPS and RNTBD
            ICommunicationEventSource eventSource = null, // required for HTTPS, not used for RNTBD
            string overrideHostNameInCertificate = null, // optional for RNTBD, not used for HTTPS
            int openTimeoutInSeconds = 0, // optional for RNTBD, not used for HTTPS
            int idleTimeoutInSeconds = 100,// optional for RNTBD, not used for HTTPS
            int timerPoolGranularityInSeconds = 0, // optional for RNTBD, not used for HTTPS
            int maxRntbdChannels = ushort.MaxValue,  // RNTBD
            int maxRequestsPerRntbdChannel = 30,  // RNTBD
            int receiveHangDetectionTimeSeconds = 65,  // RNTBD
            int sendHangDetectionTimeSeconds = 10,  // RNTBD
            Func<TransportClient, TransportClient> transportClientHandlerFactory = null // Interceptor factory
            )
        {
            if (protocol == Protocol.Https)
            {
                if (eventSource == null)
                {
                    throw new ArgumentOutOfRangeException("eventSource");
                }
                this.transportClient = new HttpTransportClient(requestTimeoutInSeconds, eventSource, userAgent);
            }
            else if (protocol == Protocol.Tcp)
            {
                if (maxRntbdChannels <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxRntbdChannels));
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
                const int minRecommendedMaxRequestsPerChannel = 10;
                const int maxRecommendedMaxRequestsPerChannel = 10000;
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
                const int minRequiredSimultaneousRequests = 10000;
                if (checked(maxRntbdChannels * maxRequestsPerRntbdChannel) <
                    minRequiredSimultaneousRequests)
                {
                    DefaultTrace.TraceCritical(
                        "The number of simultaneous requests allowed per backend " +
                        "is unreasonably small. Received {0} = {1}, {2} = {3}. " +
                        "Reasonable values are at least {4}",
                        nameof(maxRntbdChannels), maxRntbdChannels,
                        nameof(maxRequestsPerRntbdChannel), maxRequestsPerRntbdChannel,
                        minRequiredSimultaneousRequests);
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
                        MaxRequestsPerChannel = maxRequestsPerRntbdChannel,
                        ReceiveHangDetectionTime = TimeSpan.FromSeconds(receiveHangDetectionTimeSeconds),
                        SendHangDetectionTime = TimeSpan.FromSeconds(sendHangDetectionTimeSeconds),
                        UserAgent = userAgent,
                        CertificateHostNameOverride = overrideHostNameInCertificate,
                        OpenTimeout = TimeSpan.FromSeconds(openTimeoutInSeconds),
                        TimerPoolResolution = TimeSpan.FromSeconds(timerPoolGranularityInSeconds)
                    });
            }
            else
            {
                throw new ArgumentOutOfRangeException("protocol", protocol, "Invalid protocol value");
            }
            this.protocol = protocol;

            if (transportClientHandlerFactory != null)
            {
                this.fallbackClient = transportClientHandlerFactory(this.fallbackClient);
                this.transportClient = transportClientHandlerFactory(this.transportClient);
            }
        }

        public StoreClient CreateStoreClient(
            IAddressResolver addressResolver,
            ISessionContainer sessionContainer,
            IServiceConfigurationReader serviceConfigurationReader,
            IAuthorizationTokenProvider authorizationTokenProvider,
            bool enableRequestDiagnostics = false,
            bool enableReadRequestsFallback = false,
            bool useFallbackClient = true,
            bool useMultipleWriteLocations = false)
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
                    useMultipleWriteLocations);
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
                useMultipleWriteLocations);
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
    }
}
