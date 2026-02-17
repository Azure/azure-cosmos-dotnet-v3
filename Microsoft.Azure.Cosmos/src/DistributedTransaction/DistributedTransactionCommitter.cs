// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal class DistributedTransactionCommitter
    {
        // DTC-specific headers
        private const string IdempotencyTokenHeader = "x-ms-cosmos-idempotency-token";
        private const string OperationTypeHeader = "x-ms-cosmos-operation-type";
        private const string ResourceTypeHeader = "x-ms-cosmos-resource-type";
        private const string DtcResourcePath = "/operations/dtc";
        
        // Custom DTC operation and resource type values (sent as headers)
        private const string DtcOperationType = "CommitDistributedTransaction";
        private const string DtcResourceType = "DistributedTransactionBatch";

        private readonly IReadOnlyList<DistributedTransactionOperation> operations;
        private readonly CosmosClientContext clientContext;

        public DistributedTransactionCommitter(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosClientContext clientContext)
        {
            this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
        }

        public async Task<DistributedTransactionResponse> CommitTransactionAsync(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await DistributedTransactionCommitterUtils.ResolveCollectionRidsAsync(
                    this.operations,
                    this.clientContext,
                    cancellationToken);

                DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                    this.operations,
                    this.clientContext.SerializerCore,
                    cancellationToken);

                return await this.ExecuteCommitAsync(serverRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError($"Distributed transaction failed: {ex.Message}");
                // await this.AbortTransactionAsync(cancellationToken);
                throw;
            }
        }

        private async Task<DistributedTransactionResponse> ExecuteCommitAsync(
            DistributedTransactionServerRequest serverRequest,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (ITrace trace = Trace.GetRootTrace("Execute Distributed Transaction Commit", TraceComponent.Batch, TraceLevel.Info))
            {
                using (MemoryStream bodyStream = serverRequest.TransferBodyStream())
                {
                    // Build the DTC endpoint URI directly, bypassing SDK routing
                    Uri serviceEndpoint = this.clientContext.Client.Endpoint;
                    Uri dtcEndpoint = new Uri(serviceEndpoint, DtcResourcePath);

                    // Create HTTP request directly to send custom operationType and resourceType
                    ClientSideRequestStatisticsTraceDatum clientSideRequestStatistics = new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, trace);
                    
                    HttpResponseMessage httpResponse = await this.SendDtcRequestAsync(
                        dtcEndpoint,
                        bodyStream,
                        serverRequest,
                        clientSideRequestStatistics,
                        trace,
                        cancellationToken);

                    // Convert HTTP response to SDK ResponseMessage
                    ResponseMessage responseMessage = await this.CreateResponseMessageAsync(
                        httpResponse,
                        trace,
                        clientSideRequestStatistics);

                    cancellationToken.ThrowIfCancellationRequested();

                    return await DistributedTransactionResponse.FromResponseMessageAsync(
                        responseMessage,
                        serverRequest,
                        this.clientContext.SerializerCore,
                        serverRequest.IdempotencyToken,
                        trace,
                        cancellationToken);
                }
            }
        }

        private async Task<HttpResponseMessage> SendDtcRequestAsync(
            Uri endpoint,
            Stream bodyStream,
            DistributedTransactionServerRequest serverRequest,
            ClientSideRequestStatisticsTraceDatum clientSideRequestStatistics,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            // Use the same date for auth and request
            string requestDate = DateTime.UtcNow.ToString("r");
            
            // Get authorization token for the request
            string authToken = await this.GetAuthorizationTokenAsync(requestDate, trace);

            // Log request details for debugging
            string logFile = @"C:\temp\dtc_debug.log";
            System.IO.File.AppendAllText(logFile, "========== DTC REQUEST DEBUG ==========\n");
            System.IO.File.AppendAllText(logFile, $"Timestamp: {DateTime.UtcNow:O}\n");
            System.IO.File.AppendAllText(logFile, $"URI: {endpoint}\n");
            System.IO.File.AppendAllText(logFile, $"Method: POST\n");
            System.IO.File.AppendAllText(logFile, $"Headers:\n");
            System.IO.File.AppendAllText(logFile, $"  {OperationTypeHeader}: {DtcOperationType}\n");
            System.IO.File.AppendAllText(logFile, $"  {ResourceTypeHeader}: {DtcResourceType}\n");
            System.IO.File.AppendAllText(logFile, $"  {IdempotencyTokenHeader}: {serverRequest.IdempotencyToken}\n");
            System.IO.File.AppendAllText(logFile, $"  {HttpConstants.HttpHeaders.Version}: {HttpConstants.Versions.CurrentVersion}\n");
            System.IO.File.AppendAllText(logFile, $"  {HttpConstants.HttpHeaders.XDate}: {requestDate}\n");
            System.IO.File.AppendAllText(logFile, $"  Content-Type: application/json\n");
            
            // Log body
            bodyStream.Position = 0;
            using (StreamReader reader = new StreamReader(bodyStream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
            {
                string body = await reader.ReadToEndAsync();
                System.IO.File.AppendAllText(logFile, $"Body ({body.Length} chars):\n");
                System.IO.File.AppendAllText(logFile, (body.Length > 2000 ? body.Substring(0, 2000) + "..." : body) + "\n");
            }
            bodyStream.Position = 0;
            System.IO.File.AppendAllText(logFile, "========================================\n\n");

            Func<ValueTask<HttpRequestMessage>> createRequestMessage = () =>
            {
                HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
                
                // Clone body stream for the request
                MemoryStream clonedStream = new MemoryStream();
                bodyStream.Position = 0;
                bodyStream.CopyTo(clonedStream);
                clonedStream.Position = 0;
                
                httpRequest.Content = new StreamContent(clonedStream);
                httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                // Set required Cosmos DB headers - use the same date as auth token
                httpRequest.Headers.TryAddWithoutValidation(HttpConstants.HttpHeaders.Authorization, authToken);
                httpRequest.Headers.TryAddWithoutValidation(HttpConstants.HttpHeaders.XDate, requestDate);
                httpRequest.Headers.TryAddWithoutValidation(HttpConstants.HttpHeaders.Version, HttpConstants.Versions.CurrentVersion);
                
                // Set DTC-specific headers with custom operation/resource type
                httpRequest.Headers.TryAddWithoutValidation(OperationTypeHeader, DtcOperationType);
                httpRequest.Headers.TryAddWithoutValidation(ResourceTypeHeader, DtcResourceType);
                httpRequest.Headers.TryAddWithoutValidation(IdempotencyTokenHeader, serverRequest.IdempotencyToken.ToString());
                
                // Add SDK user agent
                httpRequest.Headers.TryAddWithoutValidation(HttpConstants.HttpHeaders.UserAgent, this.clientContext.UserAgent);
                httpRequest.Headers.TryAddWithoutValidation(HttpConstants.HttpHeaders.SDKSupportedCapabilities, Headers.SDKSUPPORTEDCAPABILITIES);

                return new ValueTask<HttpRequestMessage>(httpRequest);
            };

            HttpResponseMessage response = await this.clientContext.DocumentClient.httpClient.SendHttpAsync(
                createRequestMessageAsync: createRequestMessage,
                resourceType: ResourceType.Document, // Use Document as placeholder - actual type is in header
                timeoutPolicy: HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance,
                clientSideRequestStatistics: clientSideRequestStatistics,
                cancellationToken: cancellationToken);

            // Log response - buffer the content first so we can read it twice
            string responseBody = null;
            if (response.Content != null)
            {
                responseBody = await response.Content.ReadAsStringAsync();
            }
            
#pragma warning disable CS0219
            string responseLogFile = @"C:\temp\dtc_debug.log";
#pragma warning restore CS0219
            System.IO.File.AppendAllText(responseLogFile, "========== DTC RESPONSE DEBUG ==========\n");
            System.IO.File.AppendAllText(responseLogFile, $"Status: {(int)response.StatusCode} {response.StatusCode}\n");
            System.IO.File.AppendAllText(responseLogFile, $"Response Headers:\n");
            foreach (var header in response.Headers)
            {
                System.IO.File.AppendAllText(responseLogFile, $"  {header.Key}: {string.Join(", ", header.Value)}\n");
            }
            if (responseBody != null)
            {
                System.IO.File.AppendAllText(responseLogFile, $"Response Body ({responseBody.Length} chars):\n");
                System.IO.File.AppendAllText(responseLogFile, (responseBody.Length > 2000 ? responseBody.Substring(0, 2000) + "..." : responseBody) + "\n");
                
                // Replace the content with a new StringContent so it can be read again
                response.Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json");
            }
            System.IO.File.AppendAllText(responseLogFile, "=========================================\n\n");

            return response;
        }

        private async Task<string> GetAuthorizationTokenAsync(string requestDate, ITrace trace)
        {
            // Get authorization for the DTC operation
            // Server expects: 'post\ndatabaseaccount\n\n{date}\n\n' based on error message
            INameValueCollection headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.XDate, requestDate);
            return await ((ICosmosAuthorizationTokenProvider)this.clientContext.DocumentClient).GetUserAuthorizationTokenAsync(
                resourceAddress: string.Empty,
                resourceType: "databaseaccount",  // Server expects this based on error message
                requestVerb: HttpConstants.HttpMethods.Post,
                headers: headers,
                tokenType: AuthorizationTokenType.PrimaryMasterKey,
                trace: trace);
        }

        private async Task<ResponseMessage> CreateResponseMessageAsync(
            HttpResponseMessage httpResponse,
            ITrace trace,
            ClientSideRequestStatisticsTraceDatum clientSideRequestStatistics)
        {
            Stream responseStream = null;
            if (httpResponse.Content != null)
            {
                responseStream = await httpResponse.Content.ReadAsStreamAsync();
            }

            Headers responseHeaders = new Headers();
            foreach (var header in httpResponse.Headers)
            {
                responseHeaders.Add(header.Key, string.Join(",", header.Value));
            }
            if (httpResponse.Content?.Headers != null)
            {
                foreach (var header in httpResponse.Content.Headers)
                {
                    responseHeaders.Add(header.Key, string.Join(",", header.Value));
                }
            }

            return new ResponseMessage(
                statusCode: httpResponse.StatusCode,
                requestMessage: null,
                headers: responseHeaders,
                cosmosException: null,
                trace: trace)
            {
                Content = responseStream
            };
        }

        private Task AbortTransactionAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
