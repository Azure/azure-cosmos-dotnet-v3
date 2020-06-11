//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// HttpMessageHandler can only be invoked by derived classed or internal classes inside http assembly
    /// </summary>
    internal class RequestInvokerHandler : RequestHandler
    {
        private static (bool, ResponseMessage) clientIsValid = (false, null);
        private readonly CosmosClient client;
        private Cosmos.ConsistencyLevel? AccountConsistencyLevel = null;
        private Cosmos.ConsistencyLevel? RequestedClientConsistencyLevel;

        public RequestInvokerHandler(
            CosmosClient client,
            Cosmos.ConsistencyLevel? requestedClientConsistencyLevel)
        {
            this.client = client;
            this.RequestedClientConsistencyLevel = requestedClientConsistencyLevel;
        }

        public override async Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            RequestOptions promotedRequestOptions = request.RequestOptions;
            if (promotedRequestOptions != null)
            {
                // Fill request options
                promotedRequestOptions.PopulateRequestOptions(request);
            }

            await this.ValidateAndSetConsistencyLevelAsync(request);
            (bool isError, ResponseMessage errorResponse) = await this.EnsureValidClientAsync(request);
            if (isError)
            {
                return errorResponse;
            }

            await request.AssertPartitioningDetailsAsync(this.client, cancellationToken);
            this.FillMultiMasterContext(request);
            return await base.SendAsync(request, cancellationToken);
        }

        public virtual async Task<T> SendAsync<T>(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerInternal cosmosContainerCore,
            Cosmos.PartitionKey? partitionKey,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            Func<ResponseMessage, T> responseCreator,
            CosmosDiagnosticsContext diagnosticsScope,
            CancellationToken cancellationToken)
        {
            if (responseCreator == null)
            {
                throw new ArgumentNullException(nameof(responseCreator));
            }

            ResponseMessage responseMessage = await this.SendAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                cosmosContainerCore: cosmosContainerCore,
                partitionKey: partitionKey,
                streamPayload: streamPayload,
                requestEnricher: requestEnricher,
                diagnosticsContext: diagnosticsScope,
                cancellationToken: cancellationToken);

            return responseCreator(responseMessage);
        }

        public virtual async Task<ResponseMessage> SendAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerInternal cosmosContainerCore,
            Cosmos.PartitionKey? partitionKey,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (resourceUri == null)
            {
                throw new ArgumentNullException(nameof(resourceUri));
            }

            // DEVNOTE: Non-Item operations need to be refactored to always pass
            // the diagnostic context in. https://github.com/Azure/azure-cosmos-dotnet-v3/issues/1276
            IDisposable overallScope = null;
            if (diagnosticsContext == null)
            {
                diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
                overallScope = diagnosticsContext.GetOverallScope();
            }

            try
            {
                using (overallScope)
                {
                    HttpMethod method = RequestInvokerHandler.GetHttpMethod(operationType);
                    RequestMessage request = new RequestMessage(
                            method,
                            resourceUri,
                            diagnosticsContext)
                    {
                        OperationType = operationType,
                        ResourceType = resourceType,
                        RequestOptions = requestOptions,
                        Content = streamPayload,
                    };

                    if (partitionKey.HasValue)
                    {
                        if (cosmosContainerCore == null && object.ReferenceEquals(partitionKey, Cosmos.PartitionKey.None))
                        {
                            throw new ArgumentException($"{nameof(cosmosContainerCore)} can not be null with partition key as PartitionKey.None");
                        }
                        else if (partitionKey.Value.IsNone)
                        {
                            using (diagnosticsContext.CreateScope("GetNonePkValue"))
                            {
                                try
                                {
                                    PartitionKeyInternal partitionKeyInternal = await cosmosContainerCore.GetNonePartitionKeyValueAsync(cancellationToken);
                                    request.Headers.PartitionKey = partitionKeyInternal.ToJsonString();
                                }
                                catch (DocumentClientException dce)
                                {
                                    return dce.ToCosmosResponseMessage(request);
                                }
                                catch (CosmosException ce)
                                {
                                    return ce.ToCosmosResponseMessage(request);
                                }
                            }
                        }
                        else
                        {
                            request.Headers.PartitionKey = partitionKey.Value.ToJsonString();
                        }
                    }

                    if (operationType == OperationType.Upsert)
                    {
                        request.Headers.IsUpsert = bool.TrueString;
                    }

                    requestEnricher?.Invoke(request);
                    return await this.SendAsync(request, cancellationToken);
                }
            }
            catch (OperationCanceledException oe)
            {
                throw new CosmosOperationCanceledException(oe, diagnosticsContext);
            }
        }

        internal static HttpMethod GetHttpMethod(
            OperationType operationType)
        {
            HttpMethod httpMethod = HttpMethod.Head;
            if (operationType == OperationType.Create ||
                operationType == OperationType.Upsert ||
                operationType == OperationType.Query ||
                operationType == OperationType.SqlQuery ||
                operationType == OperationType.QueryPlan ||
                operationType == OperationType.Batch ||
                operationType == OperationType.ExecuteJavaScript)
            {
                return HttpMethod.Post;
            }
            else if (operationType == OperationType.Read ||
                operationType == OperationType.ReadFeed)
            {
                return HttpMethod.Get;
            }
            else if (operationType == OperationType.Replace)
            {
                return HttpMethod.Put;
            }
            else if (operationType == OperationType.Delete)
            {
                return HttpMethod.Delete;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private async Task<(bool, ResponseMessage)> EnsureValidClientAsync(RequestMessage request)
        {
            try
            {
                await this.client.DocumentClient.EnsureValidClientAsync();
                return RequestInvokerHandler.clientIsValid;
            }
            catch (DocumentClientException dce)
            {
                return (true, dce.ToCosmosResponseMessage(request));
            }
        }

        private void FillMultiMasterContext(RequestMessage request)
        {
            if (this.client.DocumentClient.UseMultipleWriteLocations)
            {
                request.Headers.Set(HttpConstants.HttpHeaders.AllowTentativeWrites, bool.TrueString);
            }
        }

        private async Task ValidateAndSetConsistencyLevelAsync(RequestMessage requestMessage)
        {
            // Validate the request consistency compatibility with account consistency
            // Type based access context for requested consistency preferred for performance
            Cosmos.ConsistencyLevel? consistencyLevel = null;
            RequestOptions promotedRequestOptions = requestMessage.RequestOptions;
            if (promotedRequestOptions != null && promotedRequestOptions.BaseConsistencyLevel.HasValue)
            {
                consistencyLevel = promotedRequestOptions.BaseConsistencyLevel;
            }
            else if (this.RequestedClientConsistencyLevel.HasValue)
            {
                consistencyLevel = this.RequestedClientConsistencyLevel;
            }

            if (consistencyLevel.HasValue)
            {
                if (!this.AccountConsistencyLevel.HasValue)
                {
                    this.AccountConsistencyLevel = await this.client.GetAccountConsistencyLevelAsync();
                }

                if (ValidationHelpers.IsValidConsistencyLevelOverwrite(this.AccountConsistencyLevel.Value, consistencyLevel.Value))
                {
                    // ConsistencyLevel compatibility with back-end configuration will be done by RequestInvokeHandler
                    requestMessage.Headers.Add(HttpConstants.HttpHeaders.ConsistencyLevel, consistencyLevel.Value.ToString());
                }
                else
                {
                    throw new ArgumentException(string.Format(
                            CultureInfo.CurrentUICulture,
                            RMResources.InvalidConsistencyLevel,
                            consistencyLevel.Value.ToString(),
                            this.AccountConsistencyLevel));
                }
            }
        }
    }
}
