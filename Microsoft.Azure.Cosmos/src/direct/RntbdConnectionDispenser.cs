//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal sealed class RntbdConnectionDispenser : IConnectionDispenser, IDisposable
    {
        private readonly int requestTimeoutInSeconds;
        private readonly int idleConnectionTimeoutInSeconds;
        private readonly string overrideHostNameInCertificate;
        private readonly int openTimeoutInSeconds;
        private readonly UserAgentContainer userAgent;
        private bool isDisposed = false;
        private TimerWheel timerPool;

        public RntbdConnectionDispenser(
            int requestTimeoutInSeconds, 
            string overrideHostNameInCertificate, 
            int openTimeoutInSeconds, 
            int idleConnectionTimeoutInSeconds, 
            int timerPoolGranularityInSeconds,
            UserAgentContainer userAgent)
        {
            this.requestTimeoutInSeconds = requestTimeoutInSeconds;
            this.overrideHostNameInCertificate = overrideHostNameInCertificate;
            this.idleConnectionTimeoutInSeconds = idleConnectionTimeoutInSeconds;
            this.openTimeoutInSeconds = openTimeoutInSeconds;
            this.userAgent = userAgent;

            int timerValueInSeconds = 0;
            if(timerPoolGranularityInSeconds > 0 && 
                timerPoolGranularityInSeconds  < openTimeoutInSeconds &&
                timerPoolGranularityInSeconds < requestTimeoutInSeconds)

            {
                timerValueInSeconds = timerPoolGranularityInSeconds;
            }
            else
            {
                if(openTimeoutInSeconds > 0 && requestTimeoutInSeconds > 0)
                {
                    timerValueInSeconds = Math.Min(openTimeoutInSeconds, requestTimeoutInSeconds);
                }
                else if (openTimeoutInSeconds > 0)
                {
                    timerValueInSeconds = openTimeoutInSeconds;
                }
                else if(requestTimeoutInSeconds > 0)
                {
                    timerValueInSeconds = requestTimeoutInSeconds;
                }
            }

            int max = Math.Max(openTimeoutInSeconds, requestTimeoutInSeconds);
            this.timerPool = TimerWheel.CreateTimerWheel(TimeSpan.FromSeconds(timerPoolGranularityInSeconds), max);
            
            DefaultTrace.TraceInformation("RntbdConnectionDispenser: requestTimeoutInSeconds: {0}, openTimeoutInSeconds: {1}, timerValueInSeconds: {2}",
                requestTimeoutInSeconds, 
                openTimeoutInSeconds,
                timerValueInSeconds);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if(disposing)
            {
                this.timerPool.Dispose();
                this.timerPool = null;
                DefaultTrace.TraceInformation("RntbdConnectionDispenser Disposed");
            }

            this.isDisposed = true;
        }

        public async Task<IConnection> OpenNewConnection(Guid activityId, Uri fullUri, string poolKey)
        {
            RntbdConnection connection;
#if DEBUG && !(NETSTANDARD15 || NETSTANDARD16 || NETSTANDARD20)
            bool sendFuzzedRequest = false;
            bool sendFuzzedContext = false;
            string sendFuzzedRequestConfig = System.Configuration.ConfigurationManager.AppSettings["SendFuzzedRequest"];
            if (!string.IsNullOrEmpty(sendFuzzedRequestConfig))
            {
                if (!bool.TryParse(sendFuzzedRequestConfig, out sendFuzzedRequest))
                {
                    sendFuzzedRequest = false;
                }
            }

            string sendFuzzedContextConfig = System.Configuration.ConfigurationManager.AppSettings["SendFuzzedContext"];
            if (!string.IsNullOrEmpty(sendFuzzedContextConfig))
            {
                if (!bool.TryParse(sendFuzzedContextConfig, out sendFuzzedContext))
                {
                    sendFuzzedContext = false;
                }
            }

            if (sendFuzzedRequest || sendFuzzedContext)
            {
                connection = new RntbdFuzzConnection(
                    fullUri, 
                    this.requestTimeoutInSeconds, 
                    this.overrideHostNameInCertificate, 
                    this.openTimeoutInSeconds, 
                    this.idleConnectionTimeoutInSeconds, 
                    poolKey, 
                    sendFuzzedRequest, 
                    sendFuzzedContext, 
                    userAgent,
                    this.timerPool);
            }
            else
#endif
            {
                connection = new RntbdConnection(
                    fullUri, 
                    this.requestTimeoutInSeconds, 
                    this.overrideHostNameInCertificate, 
                    this.openTimeoutInSeconds, 
                    this.idleConnectionTimeoutInSeconds, 
                    poolKey, 
                    userAgent,
                    this.timerPool);
            }

            DateTimeOffset creationTimestampTicks = DateTimeOffset.Now;

            try
            {
                await connection.Open(activityId, fullUri);
            }
            finally
            {
                Rntbd.ChannelOpenTimeline.LegacyWriteTrace(connection.ConnectionTimers);
            }

            return connection;
        }
    }
}
