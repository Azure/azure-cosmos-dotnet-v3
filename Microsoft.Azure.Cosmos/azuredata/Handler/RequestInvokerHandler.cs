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
    using global::Azure.Core.Pipeline;
    using global::Azure.Data.Cosmos;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// HttpMessageHandler can only be invoked by derived classed or internal classes inside http assembly
    /// </summary>
    internal class RequestInvokerHandler : RequestHandler
    {
        private static (bool, ResponseMessage) clientIsValid = (false, null);
        private readonly CosmosDriverContext clientPipelineBuilderContext;
        private Documents.ConsistencyLevel? AccountConsistencyLevel = null;
        private Documents.ConsistencyLevel? RequestedClientConsistencyLevel;

        public RequestInvokerHandler(CosmosDriverContext clientPipelineBuilderContext)
        {
            this.clientPipelineBuilderContext = clientPipelineBuilderContext;
            this.RequestedClientConsistencyLevel = clientPipelineBuilderContext.ConsistencyLevel;
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

            await request.AssertPartitioningDetailsAsync(this.clientPipelineBuilderContext.GetCollectionCacheAsync, cancellationToken);
            this.FillMultiMasterContext(request);
            return await base.SendAsync(request, cancellationToken);
        }

        internal static RequestMethod GetHttpMethod(
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
                return RequestMethod.Post;
            }
            else if (operationType == OperationType.Read ||
                operationType == OperationType.ReadFeed)
            {
                return RequestMethod.Get;
            }
            else if (operationType == OperationType.Replace)
            {
                return RequestMethod.Put;
            }
            else if (operationType == OperationType.Delete)
            {
                return RequestMethod.Delete;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public async Task<(RequestMessage, ResponseMessage)> TryCreateRequestMessageAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerCore cosmosContainerCore,
            global::Azure.Data.Cosmos.PartitionKey? partitionKey,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            CancellationToken cancellationToken)
        {
            if (resourceUri == null)
            {
                throw new ArgumentNullException(nameof(resourceUri));
            }

            RequestMethod method = RequestInvokerHandler.GetHttpMethod(operationType);

            RequestMessage request = new RequestMessage(method, resourceUri)
            {
                OperationType = operationType,
                ResourceType = resourceType,
                RequestOptions = requestOptions,
            };

            if (streamPayload != null)
            {
                request.Content = new CosmosStreamContent(streamPayload);
            }

            if (partitionKey != null)
            {
                if (cosmosContainerCore == null && Object.ReferenceEquals(partitionKey, global::Azure.Data.Cosmos.PartitionKey.None))
                {
                    throw new ArgumentException($"{nameof(cosmosContainerCore)} can not be null with partition key as PartitionKey.None");
                }
                else if (partitionKey.Value.IsNone)
                {
                    try
                    {
                        PartitionKeyInternal partitionKeyInternal = await cosmosContainerCore.GetNonePartitionKeyValueAsync(cancellationToken);
                        request.CosmosHeaders.PartitionKey = partitionKeyInternal.ToJsonString();
                    }
                    catch (DocumentClientException dce)
                    {
                        return (null, dce.ToCosmosResponseMessage(request));
                    }
                    catch (CosmosException ce)
                    {
                        return (null, ce.ToCosmosResponseMessage(request));
                    }
                }
                else
                {
                    request.CosmosHeaders.PartitionKey = partitionKey.ToString();
                }
            }

            if (operationType == OperationType.Upsert)
            {
                request.CosmosHeaders.IsUpsert = bool.TrueString;
            }

            requestEnricher?.Invoke(request);
            return (request, null);
        }

        private async Task<(bool, ResponseMessage)> EnsureValidClientAsync(RequestMessage request)
        {
            try
            {
                await this.clientPipelineBuilderContext.EnsureClientIsValidAsync();
                return RequestInvokerHandler.clientIsValid;
            }
            catch (DocumentClientException dce)
            {
                return (true, dce.ToCosmosResponseMessage(request));
            }
        }

        private void FillMultiMasterContext(RequestMessage request)
        {
            if (this.clientPipelineBuilderContext.UseMultipleWriteLocations)
            {
                request.CosmosHeaders.Set(HttpConstants.HttpHeaders.AllowTentativeWrites, bool.TrueString);
            }
        }

        private async Task ValidateAndSetConsistencyLevelAsync(RequestMessage requestMessage)
        {
            // Validate the request consistency compatibility with account consistency
            // Type based access context for requested consistency preferred for performance
            Documents.ConsistencyLevel? consistencyLevel = null;
            RequestOptions promotedRequestOptions = requestMessage.RequestOptions;
            if (promotedRequestOptions != null && promotedRequestOptions.BaseConsistencyLevel.HasValue)
            {
                consistencyLevel = (Documents.ConsistencyLevel)promotedRequestOptions.BaseConsistencyLevel;
            }
            else if (this.RequestedClientConsistencyLevel.HasValue)
            {
                consistencyLevel = this.RequestedClientConsistencyLevel;
            }

            if (consistencyLevel.HasValue)
            {
                if (!this.AccountConsistencyLevel.HasValue)
                {
                    this.AccountConsistencyLevel = await this.clientPipelineBuilderContext.GetAccountConsistencyLevelAsync();
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
