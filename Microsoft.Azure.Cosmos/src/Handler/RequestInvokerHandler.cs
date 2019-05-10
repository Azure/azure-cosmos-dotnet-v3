//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// HttpMessageHandler can only be invoked by derived classed or internal classes inside http assembly
    /// </summary>
    internal class RequestInvokerHandler : CosmosRequestHandler
    {
        private readonly CosmosClient client;

        public RequestInvokerHandler(CosmosClient client)
        {
            this.client = client;
        }

        public override Task<CosmosResponseMessage> SendAsync(
            CosmosRequestMessage request, 
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            CosmosRequestOptions promotedRequestOptions = request.RequestOptions;
            if (promotedRequestOptions != null)
            {
                // Fill request options
                promotedRequestOptions.FillRequestOptions(request);

                // Validate the request consistency compatibility with account consistency
                // Type based access context for requested consistency preferred for performance
                Cosmos.ConsistencyLevel? consistencyLevel = null;
                if (promotedRequestOptions is CosmosItemRequestOptions)
                {
                    consistencyLevel = (promotedRequestOptions as CosmosItemRequestOptions).ConsistencyLevel;
                }
                else if (promotedRequestOptions is CosmosQueryRequestOptions)
                {
                    consistencyLevel = (promotedRequestOptions as CosmosQueryRequestOptions).ConsistencyLevel;
                }
                else if (promotedRequestOptions is CosmosStoredProcedureRequestOptions)
                {
                    consistencyLevel = (promotedRequestOptions as CosmosStoredProcedureRequestOptions).ConsistencyLevel;
                }

                if (consistencyLevel.HasValue)
                {
                    if (!ValidationHelpers.ValidateConsistencyLevel(this.client.AccountConsistencyLevel, consistencyLevel.Value))
                    {
                        throw new ArgumentException(string.Format(
                                CultureInfo.CurrentUICulture,
                                RMResources.InvalidConsistencyLevel,
                                consistencyLevel.Value.ToString(),
                                this.client.AccountConsistencyLevel));
                    }
                }
            }

            return this.client.DocumentClient.EnsureValidClientAsync()
                .ContinueWith(task => request.AssertPartitioningDetailsAsync(client, cancellationToken))
                .ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        throw task.Exception;
                    }

                    this.FillMultiMasterContext(request);
                    return base.SendAsync(request, cancellationToken);
                })
                .Unwrap();
        }

        public virtual async Task<T> SendAsync<T>(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            CosmosContainerCore cosmosContainerCore,
            Object partitionKey,
            Stream streamPayload,
            Action<CosmosRequestMessage> requestEnricher,
            Func<CosmosResponseMessage, T> responseCreator,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (responseCreator == null)
            {
                throw new ArgumentNullException(nameof(responseCreator));
            }

            CosmosResponseMessage responseMessage = await this.SendAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                cosmosContainerCore: cosmosContainerCore,
                partitionKey: partitionKey,
                streamPayload: streamPayload,
                requestEnricher: requestEnricher,
                cancellation: cancellation);

            return responseCreator(responseMessage);
        }

        public virtual async Task<CosmosResponseMessage> SendAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            CosmosContainerCore cosmosContainerCore,
            Object partitionKey,
            Stream streamPayload,
            Action<CosmosRequestMessage> requestEnricher,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (resourceUri == null)
            {
                throw new ArgumentNullException(nameof(resourceUri));
            }

            HttpMethod method = RequestInvokerHandler.GetHttpMethod(operationType);

            CosmosRequestMessage request = new CosmosRequestMessage(method, resourceUri);
            request.OperationType = operationType;
            request.ResourceType = resourceType;
            request.RequestOptions = requestOptions;
            request.Content = streamPayload;

            if (partitionKey != null)
            {
                if (cosmosContainerCore == null && Object.ReferenceEquals(partitionKey, CosmosContainerSettings.NonePartitionKeyValue))
                {
                    throw new ArgumentException($"{nameof(cosmosContainerCore)} can not be null with partition key as PartitionKey.None");
                }
                else if (Object.ReferenceEquals(partitionKey, CosmosContainerSettings.NonePartitionKeyValue))
                {
                    try
                    {
                        PartitionKeyInternal partitionKeyInternal = await cosmosContainerCore.GetNonePartitionKeyValueAsync(cancellation);
                        request.Headers.PartitionKey = partitionKeyInternal.ToJsonString();
                    }
                    catch (DocumentClientException dce)
                    {
                        return dce.ToCosmosResponseMessage(request);
                    }
                }
                else
                {
                    PartitionKey pk = new PartitionKey(partitionKey);
                    request.Headers.PartitionKey = pk.InternalKey.ToJsonString();
                }
            }

            if (operationType == OperationType.Upsert)
            {
                request.Headers.IsUpsert = bool.TrueString;
            }

            requestEnricher?.Invoke(request);
            return await this.SendAsync(request, cancellation);
        }

        internal static HttpMethod GetHttpMethod(
            OperationType operationType)
        {
            HttpMethod httpMethod = HttpMethod.Head;
            if (operationType == OperationType.Create ||
                operationType == OperationType.Upsert ||
                operationType == OperationType.Query ||
                operationType == OperationType.SqlQuery ||
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

        private void FillMultiMasterContext(CosmosRequestMessage request)
        {
            if (this.client.DocumentClient.UseMultipleWriteLocations)
            {
                request.Headers.Set(HttpConstants.HttpHeaders.AllowTentativeWrites, bool.TrueString);
            }
        }
    }
}
