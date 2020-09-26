//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// HttpMessageHandler can only be invoked by derived classed or internal classes inside http assembly
    /// </summary>
    internal class RequestInvokerHandler : RequestHandler
    {
        private readonly CosmosClient client;
        private readonly Cosmos.ConsistencyLevel? RequestedClientConsistencyLevel;
        private static readonly HttpMethod httpPatchMethod = new HttpMethod(HttpConstants.HttpMethods.Patch);
        private Cosmos.ConsistencyLevel? AccountConsistencyLevel = null;

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
            try
            {
                await this.client.DocumentClient.EnsureValidClientAsync();
            }
            catch (DocumentClientException dce)
            {
                return dce.ToCosmosResponseMessage(request);
            }

            await request.AssertPartitioningDetailsAsync(this.client, cancellationToken);
            this.FillMultiMasterContext(request);
            return await base.SendAsync(request, cancellationToken);
        }

        public virtual async Task<T> SendAsync<T>(
            string resourceUri,
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
                resourceUriString: resourceUri,
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
            string resourceUriString,
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
            if (resourceUriString == null)
            {
                throw new ArgumentNullException(nameof(resourceUriString));
            }

            // DEVNOTE: Non-Item operations need to be refactored to always pass
            // the diagnostic context in. https://github.com/Azure/azure-cosmos-dotnet-v3/issues/1276
            bool disposeDiagnosticContext = false;
            if (diagnosticsContext == null)
            {
                diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
                disposeDiagnosticContext = true;
            }

            // This is needed for query where a single
            // user request might span multiple backend requests.
            // This will still have a single request id for retry scenarios
            ActivityScope activityScope = ActivityScope.CreateIfDefaultActivityId();
            Debug.Assert(activityScope == null || (activityScope != null &&
                         (operationType != OperationType.SqlQuery || operationType != OperationType.Query || operationType != OperationType.QueryPlan)),
                "There should be an activity id already set");

            try
            {
                HttpMethod method = RequestInvokerHandler.GetHttpMethod(operationType);
                RequestMessage request = new RequestMessage(
                        method,
                        resourceUriString,
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
                else if (operationType == OperationType.Patch)
                {
                    request.Headers.ContentType = RuntimeConstants.MediaTypes.JsonPatch;
                }

                requestEnricher?.Invoke(request);
                return await this.SendAsync(request, cancellationToken);
            }
            finally
            {
                if (disposeDiagnosticContext)
                {
                    diagnosticsContext.GetOverallScope().Dispose();
                }

                activityScope?.Dispose();
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
            else if (operationType == OperationType.Patch)
            {
                // There isn't support for PATCH method in .NetStandard 2.0
                return httpPatchMethod;
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
