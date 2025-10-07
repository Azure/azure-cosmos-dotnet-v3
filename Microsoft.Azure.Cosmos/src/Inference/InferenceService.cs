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
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal class InferenceService : IDisposable
    {
        private const string basePath = "dbinference.azure.com/";

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
            this.cosmosAuthorization = client.DocumentClient.cosmosAuthorization;
        }

        public async Task<IReadOnlyDictionary<TKey, TValue>> SemanticRerankAsync<TKey, TValue>(
            string renrankContext,
            IEnumerable<string> documents,
            SemanticRerankRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, this.inferenceEndpoint);
            INameValueCollection additionalHeaders = new RequestNameValueCollection();
            await this.cosmosAuthorization.AddAuthorizationHeaderAsync(
                headersCollection: additionalHeaders,
                this.inferenceEndpoint,
                HttpConstants.HttpMethods.Post,
                AuthorizationTokenType.PrimaryMasterKey);

            this.AddSemanticRerankOptionsToHeders(additionalHeaders, options);
            foreach (string key in additionalHeaders.AllKeys())
            {
                message.Headers.Add(key, additionalHeaders[key]);
            }

            var body = new
            {
                query = renrankContext,
                documents = documents.ToArray()
            };

            message.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(body));

            HttpResponseMessage responseMessage = await this.httpClient.SendAsync(message, cancellationToken);

            responseMessage.EnsureSuccessStatusCode();

            // return the content of the responsemessage as a dictonary
            string content = await responseMessage.Content.ReadAsStringAsync();
            return Newtonsoft.Json.JsonConvert.DeserializeObject<IReadOnlyDictionary<TKey, TValue>>(content);
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

        private void AddSemanticRerankOptionsToHeders(INameValueCollection headers, SemanticRerankRequestOptions options)
        {
            if (options == null)
            {
                return;
            }
            
            headers.Add("return_documents", options.ReturnDocuments.ToString());
            if (options.TopK > -1)
            {
                headers.Add("top_k", options.TopK.ToString());
            }
            if (options.BatchSize > -1)
            {
                headers.Add("batch_size", options.BatchSize.ToString());
            }
            headers.Add("sort", options.Sort.ToString());
            if (!string.IsNullOrEmpty(options.DocumentType))
            {
                headers.Add("document_type", options.DocumentType);
            }
            if (!string.IsNullOrEmpty(options.TargetPaths))
            {
                headers.Add("target_paths", options.TargetPaths);
            }
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
