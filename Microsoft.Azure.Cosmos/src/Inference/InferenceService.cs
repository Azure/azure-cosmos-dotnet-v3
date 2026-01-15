//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.FaultInjection;

    /// <summary>
    /// Provides functionality to interact with the Cosmos DB Inference Service for semantic reranking.
    /// </summary>
    internal class InferenceService : IDisposable
    {
        private const string FaultInjectionId = "FaultInjectionId";

        // Base path for the inference service endpoint.
        private const string basePath = "/inference/semanticReranking";
        // User agent string for inference requests.
        private const string inferenceUserAgent = "cosmos-inference-dotnet";
        // Default scope for AAD authentication.
        private const string inferenceServiceDefaultScope = "https://dbinference.azure.com/.default";
        private const int inferenceServiceDefaultMaxConnectionLimit = 50;

        private readonly int inferenceServiceMaxConnectionLimit;
        private readonly string inferenceServiceBaseUrl;
        private readonly Uri inferenceEndpoint;
        private readonly TimeSpan inferenceRequestTimeout;
        private readonly HttpTimeoutPolicy inferenceTimeoutPolicy;
        private readonly IChaosInterceptor chaosInterceptor;

        private HttpClient httpClient;
        private AuthorizationTokenProvider cosmosAuthorization;

        private bool disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="InferenceService"/> class.
        /// </summary>
        /// <param name="client">The CosmosClient instance.</param>
        /// <exception cref="InvalidOperationException">Thrown if AAD authentication is not used.</exception>
        public InferenceService(CosmosClient client)
        {
            this.inferenceServiceBaseUrl = ConfigurationManager.GetEnvironmentVariable<string>("AZURE_COSMOS_SEMANTIC_RERANKER_INFERENCE_ENDPOINT", null);

            if (string.IsNullOrEmpty(this.inferenceServiceBaseUrl))
            {
                throw new ArgumentNullException("Set environment variable AZURE_COSMOS_SEMANTIC_RERANKER_INFERENCE_ENDPOINT to use inference service");
            }

            this.inferenceServiceMaxConnectionLimit = ConfigurationManager.GetEnvironmentVariable<int?>(
                "AZURE_COSMOS_SEMANTIC_RERANKER_INFERENCE_SERVICE_MAX_CONNECTION_LIMIT",
                inferenceServiceDefaultMaxConnectionLimit) ?? inferenceServiceDefaultMaxConnectionLimit;

            // Get the inference timeout from client options, or use default
            this.inferenceRequestTimeout = client.ClientOptions?.InferenceRequestTimeout ?? TimeSpan.FromSeconds(5);

            // Create timeout policy for inference requests
            this.inferenceTimeoutPolicy = HttpTimeoutPolicyInference.Create(this.inferenceRequestTimeout);

            // Get fault injection interceptor if available from the DocumentClient
            this.chaosInterceptor = client.DocumentClient.ChaosInterceptor;

            // Create and configure HttpClient for inference requests.
            HttpMessageHandler httpMessageHandler = CosmosHttpClientCore.CreateHttpClientHandler(
                        gatewayModeMaxConnectionLimit: this.inferenceServiceMaxConnectionLimit,
                        webProxy: null,
                        serverCertificateCustomValidationCallback: client.DocumentClient.ConnectionPolicy.ServerCertificateCustomValidationCallback);

            this.httpClient = new HttpClient(httpMessageHandler);

            this.CreateClientHelper(this.httpClient);

            // Construct the inference service endpoint URI.
            this.inferenceEndpoint = new Uri($"{this.inferenceServiceBaseUrl}/{basePath}");

            // Ensure AAD authentication is used.
            if (client.DocumentClient.cosmosAuthorization.GetType() != typeof(AuthorizationTokenProviderTokenCredential))
            {
                throw new InvalidOperationException("InferenceService only supports AAD authentication.");
            }

            // Set up token credential for authorization.
            // This is done to ensure the correct scope, which is different than the scope of the client, is used for the inference service.
            AuthorizationTokenProviderTokenCredential defaultOperationTokenProvider = client.DocumentClient.cosmosAuthorization as AuthorizationTokenProviderTokenCredential;
            TokenCredential tokenCredential = defaultOperationTokenProvider.tokenCredential;

            this.cosmosAuthorization = new AuthorizationTokenProviderTokenCredential(
                tokenCredential: tokenCredential,
                accountEndpoint: new Uri(inferenceServiceDefaultScope),
                backgroundTokenCredentialRefreshInterval: client.ClientOptions?.TokenCredentialBackgroundRefreshInterval);
        }

        /// <summary>
        /// Sends a semantic rerank request to the inference service.
        /// </summary>
        /// <param name="rerankContext">The context/query for reranking.</param>
        /// <param name="documents">The documents to be reranked.</param>
        /// <param name="options">Optional additional options for the request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A dictionary containing the reranked results.</returns>
        public async Task<SemanticRerankResult> SemanticRerankAsync(
            string rerankContext,
            IEnumerable<string> documents,
            IDictionary<string, object> options = null,
            CancellationToken cancellationToken = default)
        {
            DateTime startDateTimeUtc = DateTime.UtcNow;
            IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> timeoutEnumerator = this.inferenceTimeoutPolicy.GetTimeoutEnumerator();
            timeoutEnumerator.MoveNext();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                (TimeSpan requestTimeout, TimeSpan delayForNextRequest) = timeoutEnumerator.Current;

                using (HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, this.inferenceEndpoint))
                {
                    // Prepare HTTP request for semantic reranking.
                    INameValueCollection additionalHeaders = new RequestNameValueCollection();
                    await this.cosmosAuthorization.AddInferenceAuthorizationHeaderAsync(
                        headersCollection: additionalHeaders,
                        this.inferenceEndpoint,
                        HttpConstants.HttpMethods.Post,
                        AuthorizationTokenType.AadToken);
                    additionalHeaders.Add(HttpConstants.HttpHeaders.UserAgent, inferenceUserAgent);

                    // Add all headers to the HTTP request.
                    foreach (string key in additionalHeaders.AllKeys())
                    {
                        message.Headers.Add(key, additionalHeaders[key]);
                    }

                    // Build the request payload.
                    Dictionary<string, object> body = this.AddSemanticRerankPayload(rerankContext, documents, options);

                    message.Content = new StringContent(
                        Newtonsoft.Json.JsonConvert.SerializeObject(body),
                        Encoding.UTF8,
                        RuntimeConstants.MediaTypes.Json);

                    // Create linked cancellation token source to honor both user cancellation and timeout policy
                    using CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cancellationTokenSource.CancelAfter(requestTimeout);

                    try
                    {
                        // Inject faults for testing if chaos interceptor is enabled
                        if (this.chaosInterceptor != null)
                        {
                            (bool hasFault, HttpResponseMessage fiResponseMessage) = await this.InjectFaultsAsync(
                                cancellationTokenSource,
                                additionalHeaders,
                                message);
                            if (hasFault)
                            {
                                fiResponseMessage.EnsureSuccessStatusCode();
                                return await SemanticRerankResult.DeserializeSemanticRerankResultAsync(fiResponseMessage);
                            }
                        }

                        // Send the request with timeout.
                        HttpResponseMessage responseMessage = await this.httpClient.SendAsync(message, cancellationTokenSource.Token);

                        // Execute OnAfterHttpSendAsync for fault injection if chaos interceptor is enabled
                        if (this.chaosInterceptor != null)
                        {
                            CancellationToken fiToken = cancellationTokenSource.Token;
                            fiToken.ThrowIfCancellationRequested();
                            await this.InjectResponseDelayAsync(additionalHeaders, fiToken);
                        }

                        responseMessage.EnsureSuccessStatusCode();

                        // Deserialize and return the response content.
                        return await SemanticRerankResult.DeserializeSemanticRerankResultAsync(responseMessage);
                    }
                    catch (OperationCanceledException operationCanceledException)
                    {
                        // Throw if the user passed in cancellation was requested
                        if (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }

                        // Convert OperationCanceledException to timeout exception when the HTTP client throws it
                        bool isOutOfRetries = !timeoutEnumerator.MoveNext();
                        if (isOutOfRetries)
                        {
                            string errorMessage = $"Inference Service Request Timeout. Start Time UTC:{startDateTimeUtc}; Total Duration:{(DateTime.UtcNow - startDateTimeUtc).TotalMilliseconds} Ms; Request Timeout {requestTimeout.TotalMilliseconds} Ms; Http Client Timeout:{this.httpClient.Timeout.TotalMilliseconds} Ms; Activity id: {System.Diagnostics.Trace.CorrelationManager.ActivityId};";
                            throw CosmosExceptionFactory.CreateRequestTimeoutException(
                                message: errorMessage,
                                headers: new Headers()
                                {
                                    ActivityId = System.Diagnostics.Trace.CorrelationManager.ActivityId.ToString()
                                },
                                innerException: operationCanceledException);
                        }
                    }
                    catch (HttpRequestException httpRequestException)
                    {
                        bool isOutOfRetries = !timeoutEnumerator.MoveNext();
                        if (isOutOfRetries)
                        {
                            string errorMessage = $"Inference Service Request Failed. Start Time UTC:{startDateTimeUtc}; Total Duration:{(DateTime.UtcNow - startDateTimeUtc).TotalMilliseconds} Ms; Activity id: {System.Diagnostics.Trace.CorrelationManager.ActivityId};";
                            throw CosmosExceptionFactory.CreateServiceUnavailableException(
                                message: errorMessage,
                                headers: new Headers()
                                {
                                    ActivityId = System.Diagnostics.Trace.CorrelationManager.ActivityId.ToString(),
                                    SubStatusCode = SubStatusCodes.TransportGenerated503
                                },
                                innerException: httpRequestException);
                        }
                    }
                }

                if (delayForNextRequest != TimeSpan.Zero)
                {
                    await Task.Delay(delayForNextRequest);
                }
            }
        }

        /// <summary>
        /// Configures the provided HttpClient with default headers and settings for inference requests.
        /// </summary>
        /// <param name="httpClient">The HttpClient to configure.</param>
        private void CreateClientHelper(HttpClient httpClient)
        {
            // Set the timeout to be higher than the configured request timeout to allow the
            // CancellationTokenSource to handle the timeout and retry logic properly
            httpClient.Timeout = this.inferenceRequestTimeout > TimeSpan.FromSeconds(5)
                ? this.inferenceRequestTimeout.Add(TimeSpan.FromSeconds(30))
                : TimeSpan.FromSeconds(35);
            httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

            // Set requested API version header for version enforcement.
            httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Version,
                HttpConstants.Versions.CurrentVersion);

            httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Accept, RuntimeConstants.MediaTypes.Json);
        }

        /// <summary>
        /// Constructs the payload for the semantic rerank request.
        /// </summary>
        /// <param name="rerankContext">The context/query for reranking.</param>
        /// <param name="documents">The documents to be reranked.</param>
        /// <param name="options">Optional additional options.</param>
        /// <returns>A dictionary representing the request payload.</returns>
        private Dictionary<string, object> AddSemanticRerankPayload(string rerankContext, IEnumerable<string> documents, IDictionary<string, object> options)
        {
            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                { "query", rerankContext },
                { "documents", documents.ToArray() }
            };

            if (options == null)
            {
                return payload;
            }

            // Add any additional options to the payload.
            foreach (string option in options.Keys)
            {
                payload.Add(option, options[option]);
            }

            return payload;
        }

        /// <summary>
        /// Injects faults for testing purposes using the chaos interceptor.
        /// </summary>
        /// <param name="cancellationTokenSource">The cancellation token source for the request.</param>
        /// <param name="headers">The request headers.</param>
        /// <param name="requestMessage">The HTTP request message.</param>
        /// <returns>A tuple indicating if a fault was injected and the fault response message.</returns>
        private async Task<(bool, HttpResponseMessage)> InjectFaultsAsync(
            CancellationTokenSource cancellationTokenSource,
            INameValueCollection headers,
            HttpRequestMessage requestMessage)
        {
            CancellationToken fiToken = cancellationTokenSource.Token;
            fiToken.ThrowIfCancellationRequested();

            // Create a DocumentServiceRequest for fault injection tracking
            using DocumentServiceRequest documentServiceRequest = DocumentServiceRequest.Create(
                OperationType.Read,
                Microsoft.Azure.Documents.ResourceType.Document,
                AuthorizationTokenType.AadToken);

            // Set a request fault injection id for rule limit tracking
            if (string.IsNullOrEmpty(headers.Get(InferenceService.FaultInjectionId)))
            {
                headers.Set(InferenceService.FaultInjectionId, Guid.NewGuid().ToString());
            }

            // Copy headers to the DocumentServiceRequest
            foreach (string key in headers.AllKeys())
            {
                documentServiceRequest.Headers.Set(key, headers[key]);
            }

            await this.chaosInterceptor.OnBeforeHttpSendAsync(documentServiceRequest, fiToken);

            (bool hasFault,
                HttpResponseMessage fiResponseMessage) = await this.chaosInterceptor.OnHttpRequestCallAsync(documentServiceRequest, fiToken);

            if (hasFault)
            {
                fiResponseMessage.RequestMessage = requestMessage;
            }

            return (hasFault, fiResponseMessage);
        }

        /// <summary>
        /// Injects response delay for testing purposes using the chaos interceptor.
        /// </summary>
        /// <param name="headers">The request headers.</param>
        /// <param name="fiToken">The fault injection cancellation token.</param>
        private async Task InjectResponseDelayAsync(INameValueCollection headers, CancellationToken fiToken)
        {
            // Create a DocumentServiceRequest for fault injection tracking
            using DocumentServiceRequest documentServiceRequest = DocumentServiceRequest.Create(
                OperationType.Read,
                Microsoft.Azure.Documents.ResourceType.Document,
                AuthorizationTokenType.AadToken);

            // Copy headers to the DocumentServiceRequest
            foreach (string key in headers.AllKeys())
            {
                documentServiceRequest.Headers.Set(key, headers[key]);
            }

            await this.chaosInterceptor.OnAfterHttpSendAsync(documentServiceRequest, fiToken);
        }

        /// <summary>
        /// Disposes managed resources used by the service.
        /// </summary>
        /// <param name="disposing">Indicates if called from Dispose.</param>
        protected void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.httpClient.Dispose();
                    this.cosmosAuthorization.Dispose();
                    this.httpClient = null;
                    this.cosmosAuthorization = null;
                }

                this.disposedValue = true;
            }
        }

        /// <summary>
        /// Disposes the service and its resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
        }
    }
}
