//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Security;
    using System.Reflection;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.FaultInjection;

    internal sealed class CosmosHttpClientCore : CosmosHttpClient
    {
        private const string FautInjecitonId = "FaultInjectionId";

        private readonly HttpClient httpClient;
        private readonly ICommunicationEventSource eventSource;
        private readonly IChaosInterceptor chaosInterceptor;

        private bool disposedValue;

        private CosmosHttpClientCore(
            HttpClient httpClient,
            HttpMessageHandler httpMessageHandler,
            ICommunicationEventSource eventSource,
            IChaosInterceptor chaosInterceptor = null)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this.eventSource = eventSource ?? throw new ArgumentNullException(nameof(eventSource));
            this.HttpMessageHandler = httpMessageHandler;
            this.chaosInterceptor = chaosInterceptor;
        }

        public override bool IsFaultInjectionClient => this.chaosInterceptor is not null;

        public override HttpMessageHandler HttpMessageHandler { get; }

        public static CosmosHttpClient CreateWithConnectionPolicy(
            ApiType apiType,
            ICommunicationEventSource eventSource,
            ConnectionPolicy connectionPolicy,
            HttpMessageHandler httpMessageHandler,
            EventHandler<SendingRequestEventArgs> sendingRequestEventArgs,
            EventHandler<ReceivedResponseEventArgs> receivedResponseEventArgs,
            IChaosInterceptor faultInjectionchaosInterceptor = null)
        {
            if (connectionPolicy == null)
            {
                throw new ArgumentNullException(nameof(connectionPolicy));
            }

            Func<HttpClient> httpClientFactory = connectionPolicy.HttpClientFactory;
            if (httpClientFactory != null)
            {
                if (sendingRequestEventArgs != null &&
                    receivedResponseEventArgs != null)
                {
                    throw new InvalidOperationException($"{nameof(connectionPolicy.HttpClientFactory)} can not be set at the same time as {nameof(sendingRequestEventArgs)} or {nameof(ReceivedResponseEventArgs)}");
                }

                HttpClient userHttpClient = httpClientFactory.Invoke() ?? throw new ArgumentNullException($"{nameof(httpClientFactory)} returned null. {nameof(httpClientFactory)} must return a HttpClient instance.");
                return CosmosHttpClientCore.CreateHelper(
                    httpClient: userHttpClient,
                    httpMessageHandler: httpMessageHandler,
                    requestTimeout: connectionPolicy.RequestTimeout,
                    userAgentContainer: connectionPolicy.UserAgentContainer,
                    apiType: apiType,
                    eventSource: eventSource);
            }

            if (httpMessageHandler == null)
            {
                httpMessageHandler = CosmosHttpClientCore.CreateHttpClientHandler(
                        gatewayModeMaxConnectionLimit: connectionPolicy.MaxConnectionLimit,
                        webProxy: null,
                        serverCertificateCustomValidationCallback: connectionPolicy.ServerCertificateCustomValidationCallback);
            }

            if (sendingRequestEventArgs != null ||
                receivedResponseEventArgs != null)
            {
                httpMessageHandler = CosmosHttpClientCore.CreateHttpMessageHandler(
                    httpMessageHandler,
                    sendingRequestEventArgs,
                    receivedResponseEventArgs);
            }

            HttpClient httpClient = new HttpClient(httpMessageHandler);

            return CosmosHttpClientCore.CreateHelper(
                httpClient: httpClient,
                httpMessageHandler: httpMessageHandler,
                requestTimeout: connectionPolicy.RequestTimeout,
                userAgentContainer: connectionPolicy.UserAgentContainer,
                apiType: apiType,
                eventSource: eventSource,
                chaosInterceptor: faultInjectionchaosInterceptor);
        }

        public static HttpMessageHandler CreateHttpClientHandler(
            int gatewayModeMaxConnectionLimit, 
            IWebProxy webProxy, 
            Func<X509Certificate2, X509Chain, SslPolicyErrors, bool> serverCertificateCustomValidationCallback)
        {
            // TODO: Remove type check and use #if NET6_0_OR_GREATER when multitargetting is possible
            Type socketHandlerType = Type.GetType("System.Net.Http.SocketsHttpHandler, System.Net.Http");

            if (socketHandlerType != null)
            {
                try
                {               
                    return CosmosHttpClientCore.CreateSocketsHttpHandlerHelper(gatewayModeMaxConnectionLimit, webProxy, serverCertificateCustomValidationCallback);
                }
                catch (Exception e)
                {
                    DefaultTrace.TraceError("Failed to create SocketsHttpHandler: {0}", e);
                }
            }
            
            return CosmosHttpClientCore.CreateHttpClientHandlerHelper(gatewayModeMaxConnectionLimit, webProxy, serverCertificateCustomValidationCallback);
        }

        public static HttpMessageHandler CreateSocketsHttpHandlerHelper(
            int gatewayModeMaxConnectionLimit, 
            IWebProxy webProxy, 
            Func<X509Certificate2, X509Chain, SslPolicyErrors, bool> serverCertificateCustomValidationCallback)
        {
            // TODO: Remove Reflection when multitargetting is possible
            Type socketHandlerType = Type.GetType("System.Net.Http.SocketsHttpHandler, System.Net.Http");

            object socketHttpHandler = Activator.CreateInstance(socketHandlerType);

            PropertyInfo pooledConnectionLifetimeInfo = socketHandlerType.GetProperty("PooledConnectionLifetime");

            //Sets the timeout for unused connections to a random time between 5 minutes and 5 minutes and 30 seconds.
            //This is to avoid the issue where a large number of connections are closed at the same time.
            TimeSpan connectionTimeSpan = TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(30 * CustomTypeExtensions.GetRandomNumber().NextDouble());
            pooledConnectionLifetimeInfo.SetValue(socketHttpHandler, connectionTimeSpan);

            // Proxy is only set by users and can cause not supported exception on some platforms
            if (webProxy != null)
            {
                PropertyInfo webProxyInfo = socketHandlerType.GetProperty("Proxy");
                webProxyInfo.SetValue(socketHttpHandler, webProxy);
            }

            // https://docs.microsoft.com/en-us/archive/blogs/timomta/controlling-the-number-of-outgoing-connections-from-httpclient-net-core-or-full-framework
            try
            {
                PropertyInfo maxConnectionsPerServerInfo = socketHandlerType.GetProperty("MaxConnectionsPerServer");
                maxConnectionsPerServerInfo.SetValue(socketHttpHandler, gatewayModeMaxConnectionLimit);              
            }
            // MaxConnectionsPerServer is not supported on some platforms.
            catch (PlatformNotSupportedException)
            {
            }

            if (serverCertificateCustomValidationCallback != null)
            {
                //Get SslOptions Property
                PropertyInfo sslOptionsInfo = socketHandlerType.GetProperty("SslOptions");
                object sslOptions = sslOptionsInfo.GetValue(socketHttpHandler);

                //Set SslOptions Property with custom certificate validation
                PropertyInfo remoteCertificateValidationCallbackInfo = sslOptions.GetType().GetProperty("RemoteCertificateValidationCallback");
                remoteCertificateValidationCallbackInfo.SetValue(
                    sslOptions,
                    new RemoteCertificateValidationCallback((object _, X509Certificate certificate, X509Chain x509Chain, SslPolicyErrors sslPolicyErrors) => serverCertificateCustomValidationCallback(
                            certificate is { } ? new X509Certificate2(certificate) : null,
                            x509Chain,
                            sslPolicyErrors)));
            }

            return (HttpMessageHandler)socketHttpHandler;
        }

        public static HttpMessageHandler CreateHttpClientHandlerHelper(
            int gatewayModeMaxConnectionLimit, 
            IWebProxy webProxy, 
            Func<X509Certificate2, X509Chain, SslPolicyErrors, bool> serverCertificateCustomValidationCallback)
        {
            HttpClientHandler httpClientHandler = new HttpClientHandler();

            // Proxy is only set by users and can cause not supported exception on some platforms
            if (webProxy != null)
            {
                httpClientHandler.Proxy = webProxy;
            }

            // https://docs.microsoft.com/en-us/archive/blogs/timomta/controlling-the-number-of-outgoing-connections-from-httpclient-net-core-or-full-framework
            try
            {
                httpClientHandler.MaxConnectionsPerServer = gatewayModeMaxConnectionLimit;               
            }
            // MaxConnectionsPerServer is not supported on some platforms.
            catch (PlatformNotSupportedException)
            {
            }

            if (serverCertificateCustomValidationCallback != null)
            {
                httpClientHandler.ServerCertificateCustomValidationCallback = (_, certificate2, x509Chain, sslPolicyErrors) => serverCertificateCustomValidationCallback(certificate2, x509Chain, sslPolicyErrors);
            }

            return httpClientHandler;
        }

        private static HttpMessageHandler CreateHttpMessageHandler(
            HttpMessageHandler innerHandler,
            EventHandler<SendingRequestEventArgs> sendingRequestEventArgs,
            EventHandler<ReceivedResponseEventArgs> receivedResponseEventArgs)
        {
            return new HttpRequestMessageHandler(
                sendingRequestEventArgs,
                receivedResponseEventArgs,
                innerHandler);
        }

        private static CosmosHttpClient CreateHelper(
            HttpClient httpClient,
            HttpMessageHandler httpMessageHandler,
            TimeSpan requestTimeout,
            UserAgentContainer userAgentContainer,
            ApiType apiType,
            ICommunicationEventSource eventSource,
            IChaosInterceptor chaosInterceptor = null)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException(nameof(httpClient));
            }

            httpClient.Timeout = requestTimeout > CosmosHttpClientCore.GatewayRequestTimeout
                ? requestTimeout
                : CosmosHttpClientCore.GatewayRequestTimeout;
            httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

            httpClient.AddUserAgentHeader(userAgentContainer);
            httpClient.AddApiTypeHeader(apiType);

            // Set requested API version header that can be used for
            // version enforcement.
            httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Version,
                HttpConstants.Versions.CurrentVersion);

            httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.SDKSupportedCapabilities,
                Headers.SDKSUPPORTEDCAPABILITIES);

            httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Accept, RuntimeConstants.MediaTypes.Json);

            return new CosmosHttpClientCore(
                httpClient,
                httpMessageHandler,
                eventSource,
                chaosInterceptor);
        }

        public override Task<HttpResponseMessage> GetAsync(
            Uri uri,
            INameValueCollection additionalHeaders,
            ResourceType resourceType,
            HttpTimeoutPolicy timeoutPolicy,
            IClientSideRequestStatistics clientSideRequestStatistics,
            CancellationToken cancellationToken,
            DocumentServiceRequest documentServiceRequest = null)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            // GetAsync doesn't let clients to pass in additional headers. So, we are
            // internally using SendAsync and add the additional headers to requestMessage. 
            ValueTask<HttpRequestMessage> CreateRequestMessage()
            {
                HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
                if (additionalHeaders != null)
                {
                    foreach (string header in additionalHeaders)
                    {
                        if (GatewayStoreClient.IsAllowedRequestHeader(header))
                        {
                            requestMessage.Headers.TryAddWithoutValidation(header, additionalHeaders[header]);
                        }
                    }
                }

                return new ValueTask<HttpRequestMessage>(requestMessage);
            }

            return this.SendHttpAsync(
                CreateRequestMessage,
                resourceType,
                timeoutPolicy,
                clientSideRequestStatistics,
                cancellationToken,
                documentServiceRequest);
        }

        public override Task<HttpResponseMessage> SendHttpAsync(
            Func<ValueTask<HttpRequestMessage>> createRequestMessageAsync,
            ResourceType resourceType,
            HttpTimeoutPolicy timeoutPolicy,
            IClientSideRequestStatistics clientSideRequestStatistics,
            CancellationToken cancellationToken,
            DocumentServiceRequest documentServiceRequest = null)
        {
            if (createRequestMessageAsync == null)
            {
                throw new ArgumentNullException(nameof(createRequestMessageAsync));
            }

            return this.SendHttpHelperAsync(
                createRequestMessageAsync,
                resourceType,
                timeoutPolicy,
                clientSideRequestStatistics,
                cancellationToken,
                documentServiceRequest);
        }

        private async Task<HttpResponseMessage> SendHttpHelperAsync(
            Func<ValueTask<HttpRequestMessage>> createRequestMessageAsync,
            ResourceType resourceType,
            HttpTimeoutPolicy timeoutPolicy,
            IClientSideRequestStatistics clientSideRequestStatistics,
            CancellationToken cancellationToken,
            DocumentServiceRequest documentServiceRequest)
        {
            DateTime startDateTimeUtc = DateTime.UtcNow;
            IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> timeoutEnumerator = timeoutPolicy.GetTimeoutEnumerator();
            timeoutEnumerator.MoveNext();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                (TimeSpan requestTimeout, TimeSpan delayForNextRequest) = timeoutEnumerator.Current;
                using (HttpRequestMessage requestMessage = await createRequestMessageAsync())
                {
                    // If the default cancellation token is passed then use the timeout policy
                    using CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cancellationTokenSource.CancelAfter(requestTimeout);
                    DateTime requestStartTime = DateTime.UtcNow;
                    try
                    {
                        if (this.chaosInterceptor != null && documentServiceRequest != null)
                        {
                            (bool hasFault, HttpResponseMessage fiResponseMessage) = await this.InjectFaultsAsync(cancellationTokenSource, documentServiceRequest, requestMessage);
                            if (hasFault)
                            {
                                return fiResponseMessage;
                            }
                        }

                        HttpResponseMessage responseMessage = await this.ExecuteHttpHelperAsync(
                            requestMessage,
                            resourceType,
                            cancellationTokenSource.Token);

                        if (this.chaosInterceptor != null && documentServiceRequest != null)
                        {
                            CancellationToken fiToken = cancellationTokenSource.Token;
                            fiToken.ThrowIfCancellationRequested();
                            await this.chaosInterceptor.OnAfterHttpSendAsync(documentServiceRequest);
                        }

                        if (clientSideRequestStatistics is ClientSideRequestStatisticsTraceDatum datum)
                        {
                            datum.RecordHttpResponse(requestMessage, responseMessage, resourceType, requestStartTime);
                        }

                        if (!timeoutPolicy.ShouldRetryBasedOnResponse(requestMessage.Method, responseMessage))
                        {
                            return responseMessage;
                        }

                        bool isOutOfRetries = CosmosHttpClientCore.IsOutOfRetries(timeoutPolicy, startDateTimeUtc, timeoutEnumerator);
                        if (isOutOfRetries)
                        {
                            return responseMessage;
                        }
                    }
                    catch (Exception e)
                    {
                        ITrace trace = NoOpTrace.Singleton;
                        if (clientSideRequestStatistics is ClientSideRequestStatisticsTraceDatum datum)
                        {
                            datum.RecordHttpException(requestMessage, e, resourceType, requestStartTime);
                            trace = datum.Trace;
                        }
                        bool isOutOfRetries = CosmosHttpClientCore.IsOutOfRetries(timeoutPolicy, startDateTimeUtc, timeoutEnumerator);

                        switch (e)
                        {
                            case OperationCanceledException operationCanceledException:
                                // Throw if the user passed in cancellation was requested
                                if (cancellationToken.IsCancellationRequested)
                                {
                                    throw;
                                }

                                // Convert OperationCanceledException to 408 when the HTTP client throws it. This makes it clear that the 
                                // the request timed out and was not user canceled operation.
                                if (isOutOfRetries || !timeoutPolicy.IsSafeToRetry(requestMessage.Method))
                                {
                                    // throw current exception (caught in transport handler)
                                    string message =
                                            $"GatewayStoreClient Request Timeout. Start Time UTC:{startDateTimeUtc}; Total Duration:{(DateTime.UtcNow - startDateTimeUtc).TotalMilliseconds} Ms; Request Timeout {requestTimeout.TotalMilliseconds} Ms; Http Client Timeout:{this.httpClient.Timeout.TotalMilliseconds} Ms; Activity id: {System.Diagnostics.Trace.CorrelationManager.ActivityId};";
                                    e.Data.Add("Message", message);
                                    
                                    if (timeoutPolicy.ShouldThrow503OnTimeout)
                                    {
                                        throw CosmosExceptionFactory.CreateServiceUnavailableException(
                                            message: message,
                                            headers: new Headers()
                                            {
                                                ActivityId = System.Diagnostics.Trace.CorrelationManager.ActivityId.ToString(),
                                                SubStatusCode = SubStatusCodes.TransportGenerated503
                                            },
                                            trace: trace,
                                            innerException: e);
                                    }

                                    throw;
                                }

                                break;
                            case WebException webException:
                                if (isOutOfRetries || (!timeoutPolicy.IsSafeToRetry(requestMessage.Method) && !WebExceptionUtility.IsWebExceptionRetriable(webException)))
                                {
                                    throw;
                                }

                                break;
                            case HttpRequestException httpRequestException:
                                if (isOutOfRetries || !timeoutPolicy.IsSafeToRetry(requestMessage.Method))
                                {
                                    throw;
                                }

                                break;
                            default:
                                throw;
                        }
                    }

                }

                if (delayForNextRequest != TimeSpan.Zero)
                {
                    await Task.Delay(delayForNextRequest);
                }
            }
        }

        private async Task<(bool, HttpResponseMessage)> InjectFaultsAsync(
            CancellationTokenSource cancellationTokenSource, 
            DocumentServiceRequest documentServiceRequest, 
            HttpRequestMessage requestMessage)
        {
            CancellationToken fiToken = cancellationTokenSource.Token;
            fiToken.ThrowIfCancellationRequested();

            //Set a request fault injeciton id for rule limit tracking
            if (string.IsNullOrEmpty(documentServiceRequest.Headers.Get(CosmosHttpClientCore.FautInjecitonId)))
            {
                documentServiceRequest.Headers.Set(CosmosHttpClientCore.FautInjecitonId, Guid.NewGuid().ToString());
            }
            await this.chaosInterceptor.OnBeforeHttpSendAsync(documentServiceRequest);

            (bool hasFault,
                HttpResponseMessage fiResponseMessage) = await this.chaosInterceptor.OnHttpRequestCallAsync(documentServiceRequest);

            if (hasFault)
            {
                fiResponseMessage.RequestMessage = requestMessage;
            }
            return (hasFault, fiResponseMessage);
        }

        private static bool IsOutOfRetries(
            HttpTimeoutPolicy timeoutPolicy,
            DateTime startDateTimeUtc,
            IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> timeoutEnumerator)
        {
            return !timeoutEnumerator.MoveNext(); // No more retries are configured
        }

        private async Task<HttpResponseMessage> ExecuteHttpHelperAsync(
            HttpRequestMessage requestMessage,
            ResourceType resourceType,
            CancellationToken cancellationToken)
        {
            DateTime sendTimeUtc = DateTime.UtcNow;
            Guid localGuid = Guid.NewGuid(); // For correlating HttpRequest and HttpResponse Traces

            Guid requestedActivityId = System.Diagnostics.Trace.CorrelationManager.ActivityId;
            this.eventSource.Request(
                requestedActivityId,
                localGuid,
                requestMessage.RequestUri.ToString(),
                resourceType.ToResourceTypeString(),
                requestMessage.Headers);

            // Only read the header initially. The content gets copied into a memory stream later
            // if we read the content HTTP client will buffer the message and then it will get buffered
            // again when it is copied to the memory stream.
            HttpResponseMessage responseMessage = await this.httpClient.SendAsync(
                    requestMessage,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            // WebAssembly HttpClient does not set the RequestMessage property on SendAsync
            if (responseMessage.RequestMessage == null)
            {
                responseMessage.RequestMessage = requestMessage;
            }

            DateTime receivedTimeUtc = DateTime.UtcNow;
            TimeSpan durationTimeSpan = receivedTimeUtc - sendTimeUtc;

            Guid activityId = Guid.Empty;
            if (responseMessage.Headers.TryGetValues(
                HttpConstants.HttpHeaders.ActivityId,
                out IEnumerable<string> headerValues) && headerValues.Any())
            {
                activityId = new Guid(headerValues.First());
            }

            this.eventSource.Response(
                activityId,
                localGuid,
                (short)responseMessage.StatusCode,
                durationTimeSpan.TotalMilliseconds,
                responseMessage.Headers);

            return responseMessage;
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.httpClient.Dispose();
                }

                this.disposedValue = true;
            }
        }

        public override void Dispose()
        {
            this.Dispose(true);
        }

        private class HttpRequestMessageHandler : DelegatingHandler
        {
            private readonly EventHandler<SendingRequestEventArgs> sendingRequest;
            private readonly EventHandler<ReceivedResponseEventArgs> receivedResponse;

            public HttpRequestMessageHandler(
                EventHandler<SendingRequestEventArgs> sendingRequest,
                EventHandler<ReceivedResponseEventArgs> receivedResponse,
                HttpMessageHandler innerHandler)
            {
                this.sendingRequest = sendingRequest;
                this.receivedResponse = receivedResponse;

                this.InnerHandler = innerHandler ?? throw new ArgumentNullException(
                    $"innerHandler is null. This required for .NET core to limit the http connection. See {nameof(CreateHttpClientHandler)} ");
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                this.sendingRequest?.Invoke(this, new SendingRequestEventArgs(request));
                HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
                this.receivedResponse?.Invoke(this, new ReceivedResponseEventArgs(request, response));
                return response;
            }
        }
    }
}
