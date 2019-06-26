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
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// HttpMessageHandler can only be invoked by derived classed or internal classes inside http assembly
    /// </summary>
    internal class RequestInvokerHandler : RequestHandler
    {
        private readonly CosmosClient client;

        public RequestInvokerHandler(CosmosClient client)
        {
            this.client = client;
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

                // Validate the request consistency compatibility with account consistency
                // Type based access context for requested consistency preferred for performance
                Cosmos.ConsistencyLevel? consistencyLevel = null;
                if (promotedRequestOptions is ItemRequestOptions)
                {
                    consistencyLevel = (promotedRequestOptions as ItemRequestOptions).ConsistencyLevel;
                }
                else if (promotedRequestOptions is QueryRequestOptions)
                {
                    consistencyLevel = (promotedRequestOptions as QueryRequestOptions).ConsistencyLevel;
                }
                else if (promotedRequestOptions is StoredProcedureRequestOptions)
                {
                    consistencyLevel = (promotedRequestOptions as StoredProcedureRequestOptions).ConsistencyLevel;
                }

                if (consistencyLevel.HasValue)
                {
                    Cosmos.ConsistencyLevel accountConsistency = await this.client.GetAccountConsistencyLevelAsync();
                    if (!ValidationHelpers.ValidateConsistencyLevel(accountConsistency, consistencyLevel.Value))
                    {
                        throw new ArgumentException(string.Format(
                                CultureInfo.CurrentUICulture,
                                RMResources.InvalidConsistencyLevel,
                                consistencyLevel.Value.ToString(),
                                accountConsistency));
                    }
                }
            }

            await this.client.DocumentClient.EnsureValidClientAsync();
            await request.AssertPartitioningDetailsAsync(this.client, cancellationToken);
            this.FillMultiMasterContext(request);
            return await base.SendAsync(request, cancellationToken);
        }

        public virtual async Task<T> SendAsync<T>(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerCore cosmosContainerCore,
            Cosmos.PartitionKey partitionKey,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            Func<ResponseMessage, T> responseCreator,
            CancellationToken cancellationToken = default(CancellationToken))
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
                cancellationToken: cancellationToken);

            return responseCreator(responseMessage);
        }

        public virtual async Task<ResponseMessage> SendAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerCore cosmosContainerCore,
            Cosmos.PartitionKey partitionKey,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (resourceUri == null)
            {
                throw new ArgumentNullException(nameof(resourceUri));
            }

            HttpMethod method = RequestInvokerHandler.GetHttpMethod(operationType);

            RequestMessage request = new RequestMessage(method, resourceUri)
            {
                OperationType = operationType,
                ResourceType = resourceType,
                RequestOptions = requestOptions,
                Content = streamPayload
            };

            if (partitionKey != null)
            {
                if (cosmosContainerCore == null && Object.ReferenceEquals(partitionKey, Cosmos.PartitionKey.None))
                {
                    throw new ArgumentException($"{nameof(cosmosContainerCore)} can not be null with partition key as PartitionKey.None");
                }
                else if (Object.ReferenceEquals(partitionKey, Cosmos.PartitionKey.None))
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
                }
                else
                {
                    request.Headers.PartitionKey = partitionKey.ToString();
                }
            }

            if (operationType == OperationType.Upsert)
            {
                request.Headers.IsUpsert = bool.TrueString;
            }

            requestEnricher?.Invoke(request);
            return await this.SendAsync(request, cancellationToken);
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

        private void FillMultiMasterContext(RequestMessage request)
        {
            if (this.client.DocumentClient.UseMultipleWriteLocations)
            {
                request.Headers.Set(HttpConstants.HttpHeaders.AllowTentativeWrites, bool.TrueString);
            }
        }
    }
}
