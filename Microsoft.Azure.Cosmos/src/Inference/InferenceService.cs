﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    /// <summary>
    /// Provides functionality to interact with the Cosmos DB Inference Service for semantic reranking.
    /// </summary>
    internal class InferenceService : IDisposable
    {
        // Base path for the inference service endpoint.
        private const string basePath = "dbinference.azure.com/inference/semanticReranking";
        // User agent string for inference requests.
        private const string inferenceUserAgent = "cosmos-inference-dotnet";
        // Default scope for AAD authentication.
        private const string inferenceServiceDefaultScope = "https://dbinference.azure.com/.default";

        private readonly Uri inferenceEndpoint;
        private readonly HttpClient httpClient;
        private readonly AuthorizationTokenProvider cosmosAuthorization;

        private bool disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="InferenceService"/> class.
        /// </summary>
        /// <param name="client">The CosmosClient instance.</param>
        /// <param name="accountProperties">The account properties for endpoint construction.</param>
        /// <exception cref="InvalidOperationException">Thrown if AAD authentication is not used.</exception>
        public InferenceService(CosmosClient client, AccountProperties accountProperties)
        {
            // Create and configure HttpClient for inference requests.
            HttpMessageHandler httpMessageHandler = CosmosHttpClientCore.CreateHttpClientHandler(
                        gatewayModeMaxConnectionLimit: client.DocumentClient.ConnectionPolicy.MaxConnectionLimit,
                        webProxy: null,
                        serverCertificateCustomValidationCallback: client.DocumentClient.ConnectionPolicy.ServerCertificateCustomValidationCallback);

            this.httpClient = new HttpClient(httpMessageHandler);

            this.CreateClientHelper(this.httpClient);

            // Construct the inference service endpoint URI.
            this.inferenceEndpoint = new Uri($"https://{accountProperties.Id}.{basePath}");

            // Ensure AAD authentication is used.
            if (client.DocumentClient.cosmosAuthorization.GetType() != typeof(AuthorizationTokenProviderTokenCredential))
            {
                throw new InvalidOperationException("InferenceService only supports AAD authentication.");
            }

            // Set up token credential for authorization.
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
        /// <param name="renrankContext">The context/query for reranking.</param>
        /// <param name="documents">The documents to be reranked.</param>
        /// <param name="options">Optional additional options for the request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A dictionary containing the reranked results.</returns>
        public async Task<IReadOnlyDictionary<string, dynamic>> SemanticRerankAsync(
            string renrankContext,
            IEnumerable<string> documents,
            IDictionary<string, dynamic> options = null,
            CancellationToken cancellationToken = default)
        {
            // Prepare HTTP request for semantic reranking.
            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, this.inferenceEndpoint);
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
            Dictionary<string, dynamic> body = this.AddSemanticRerankPayload(renrankContext, documents, options);

            message.Content = new StringContent(
                Newtonsoft.Json.JsonConvert.SerializeObject(body),
                Encoding.UTF8,
                RuntimeConstants.MediaTypes.Json);

            // Send the request and ensure success.
            HttpResponseMessage responseMessage = await this.httpClient.SendAsync(message, cancellationToken);
            responseMessage.EnsureSuccessStatusCode();

            // Deserialize and return the response content as a dictionary.
            string content = await responseMessage.Content.ReadAsStringAsync();
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(content);
        }

        /// <summary>
        /// Configures the provided HttpClient with default headers and settings for inference requests.
        /// </summary>
        /// <param name="httpClient">The HttpClient to configure.</param>
        private void CreateClientHelper(HttpClient httpClient)
        {
            httpClient.Timeout = TimeSpan.FromSeconds(120);
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
        private Dictionary<string, dynamic> AddSemanticRerankPayload(string rerankContext, IEnumerable<string> documents, IDictionary<string, dynamic> options)
        {
            Dictionary<string, dynamic> payload = new Dictionary<string, dynamic>
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
