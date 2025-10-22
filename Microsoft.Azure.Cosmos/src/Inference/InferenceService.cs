//------------------------------------------------------------
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

    internal class InferenceService : IDisposable
    {
        private const string basePath = "dbinference.azure.com/inference/semanticReranking";
        private const string inferenceUserAgent = "cosmos-inference-dotnet";
        private const string inferenceServiceDefaultScope = "https://dbinference.azure.com/.default";

        private readonly Uri inferenceEndpoint;
        private readonly HttpClient httpClient;
        private readonly AuthorizationTokenProvider cosmosAuthorization;

        private bool disposedValue;

        public InferenceService(CosmosClient client, AccountProperties accountProperties)
        {
            //Create HttpClient 
            HttpMessageHandler httpMessageHandler = CosmosHttpClientCore.CreateHttpClientHandler(
                        gatewayModeMaxConnectionLimit: client.DocumentClient.ConnectionPolicy.MaxConnectionLimit,
                        webProxy: null,
                        serverCertificateCustomValidationCallback: client.DocumentClient.ConnectionPolicy.ServerCertificateCustomValidationCallback);

            this.httpClient = new HttpClient(httpMessageHandler);

            this.CreateClientHelper(this.httpClient);

            //Set endpoints
            this.inferenceEndpoint = new Uri($"https://{accountProperties.Id}.{basePath}");

            //set authorization
            if (client.DocumentClient.cosmosAuthorization.GetType() != typeof(AuthorizationTokenProviderTokenCredential))
            {
                throw new InvalidOperationException("InferenceService only supports AAD authentication.");
            }

            AuthorizationTokenProviderTokenCredential defaultOperationTokenProvider = client.DocumentClient.cosmosAuthorization as AuthorizationTokenProviderTokenCredential;
            TokenCredential tokenCredential = defaultOperationTokenProvider.tokenCredential;

            this.cosmosAuthorization = new AuthorizationTokenProviderTokenCredential(
                tokenCredential: tokenCredential,
                accountEndpoint: new Uri(inferenceServiceDefaultScope),
                backgroundTokenCredentialRefreshInterval: client.ClientOptions?.TokenCredentialBackgroundRefreshInterval);
        }

        public async Task<IReadOnlyDictionary<string, dynamic>> SemanticRerankAsync(
            string renrankContext,
            IEnumerable<string> documents,
            SemanticRerankRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, this.inferenceEndpoint);
            INameValueCollection additionalHeaders = new RequestNameValueCollection();
            await this.cosmosAuthorization.AddInferenceAuthorizationHeaderAsync(
                headersCollection: additionalHeaders,
                this.inferenceEndpoint,
                HttpConstants.HttpMethods.Post,
                AuthorizationTokenType.AadToken);
            additionalHeaders.Add(HttpConstants.HttpHeaders.UserAgent, inferenceUserAgent);

            foreach (string key in additionalHeaders.AllKeys())
            {
                message.Headers.Add(key, additionalHeaders[key]);
            }

            Dictionary<string, dynamic> body = this.AddSemanticRerankPayload(renrankContext, documents, options);

            message.Content = new StringContent(
                Newtonsoft.Json.JsonConvert.SerializeObject(body),
                Encoding.UTF8,
                RuntimeConstants.MediaTypes.Json);

            HttpResponseMessage responseMessage = await this.httpClient.SendAsync(message, cancellationToken);
            responseMessage.EnsureSuccessStatusCode();

            // return the content of the responsemessage as a dictonary
            string content = await responseMessage.Content.ReadAsStringAsync();
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(content);
        }

        private void CreateClientHelper(HttpClient httpClient)
        {
            httpClient.Timeout = TimeSpan.FromSeconds(120);
            httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

            // Set requested API version header that can be used for
            // version enforcement.
            httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Version,
                HttpConstants.Versions.CurrentVersion);

            httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Accept, RuntimeConstants.MediaTypes.Json);
        }

        private Dictionary<string, dynamic> AddSemanticRerankPayload(string rerankContext, IEnumerable<string> documents, SemanticRerankRequestOptions options)
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

            payload["return_documents"] = options.ReturnDocuments;
            if (options.TopK > -1)
            {
                payload["top_k"] = options.TopK;
            }
            if (options.BatchSize > -1)
            {
                payload["batch_size"] = options.BatchSize;
            }
            payload["sort"] = options.Sort;
            if (!string.IsNullOrEmpty(options.DocumentType))
            {
                payload["document_type"] = options.DocumentType;
            }
            if (!string.IsNullOrEmpty(options.TargetPaths))
            {
                payload["target_paths"] = options.TargetPaths;
            }

            return payload;
        }

        protected void Dispose(bool disposing)
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

        public void Dispose()
        {
            this.Dispose(true);
        }
    }
}
