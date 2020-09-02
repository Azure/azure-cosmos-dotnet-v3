//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Documents;

    internal class CosmosOffers
    {
        private readonly CosmosClientContext ClientContext;
        private readonly string OfferRootUri = Paths.Offers_Root;

        public CosmosOffers(CosmosClientContext clientContext)
        {
            this.ClientContext = clientContext;
        }

        internal async Task<ThroughputResponse> ReadThroughputAsync(
            string targetRID,
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            OfferV2 offerV2 = await this.GetOfferV2Async<OfferV2>(targetRID, failIfNotConfigured: true, cancellationToken: cancellationToken);

            return await this.GetThroughputResponseAsync(
                streamPayload: null,
                operationType: OperationType.Read,
                linkUri: new Uri(offerV2.SelfLink, UriKind.Relative),
                resourceType: ResourceType.Offer,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        internal async Task<ThroughputResponse> ReadThroughputIfExistsAsync(
            string targetRID,
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            OfferV2 offerV2 = await this.GetOfferV2Async<OfferV2>(targetRID, failIfNotConfigured: false, cancellationToken: cancellationToken);

            if (offerV2 == null)
            {
                return new ThroughputResponse(
                    HttpStatusCode.NotFound,
                    headers: null,
                    throughputProperties: null,
                    diagnostics: null);
            }

            return await this.GetThroughputResponseAsync(
                streamPayload: null,
                operationType: OperationType.Read,
                linkUri: new Uri(offerV2.SelfLink, UriKind.Relative),
                resourceType: ResourceType.Offer,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        internal async Task<ThroughputResponse> ReplaceThroughputPropertiesAsync(
            string targetRID,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            ThroughputProperties currentProperty = await this.GetOfferV2Async<ThroughputProperties>(targetRID, failIfNotConfigured: true, cancellationToken: cancellationToken);
            currentProperty.Content = throughputProperties.Content;

            return await this.GetThroughputResponseAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(currentProperty),
                operationType: OperationType.Replace,
                linkUri: new Uri(currentProperty.SelfLink, UriKind.Relative),
                resourceType: ResourceType.Offer,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        internal async Task<ThroughputResponse> ReplaceThroughputPropertiesIfExistsAsync(
            string targetRID,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                ThroughputProperties currentProperty = await this.GetOfferV2Async<ThroughputProperties>(targetRID, failIfNotConfigured: false, cancellationToken: cancellationToken);

                if (currentProperty == null)
                {
                    CosmosException notFound = CosmosExceptionFactory.CreateNotFoundException(
                         $"Throughput is not configured for {targetRID}");
                    return new ThroughputResponse(
                        httpStatusCode: notFound.StatusCode,
                        headers: notFound.Headers,
                        throughputProperties: null,
                        diagnostics: notFound.Diagnostics);
                }

                currentProperty.Content = throughputProperties.Content;

                return await this.GetThroughputResponseAsync(
                    streamPayload: this.ClientContext.SerializerCore.ToStream(currentProperty),
                    operationType: OperationType.Replace,
                    linkUri: new Uri(currentProperty.SelfLink, UriKind.Relative),
                    resourceType: ResourceType.Offer,
                    requestOptions: requestOptions,
                    cancellationToken: cancellationToken);
            }
            catch (DocumentClientException dce)
            {
                ResponseMessage responseMessage = dce.ToCosmosResponseMessage(null);
                return new ThroughputResponse(
                    responseMessage.StatusCode,
                    headers: responseMessage.Headers,
                    throughputProperties: null,
                    diagnostics: responseMessage.Diagnostics);
            }
            catch (AggregateException ex)
            {
                ResponseMessage responseMessage = TransportHandler.AggregateExceptionConverter(ex, null);
                return new ThroughputResponse(
                    responseMessage.StatusCode,
                    headers: responseMessage.Headers,
                    throughputProperties: null,
                    diagnostics: responseMessage.Diagnostics);
            }
        }

        internal Task<ThroughputResponse> ReplaceThroughputAsync(
            string targetRID,
            int throughput,
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ReplaceThroughputPropertiesAsync(
                targetRID,
                ThroughputProperties.CreateManualThroughput(throughput),
                requestOptions,
                cancellationToken);
        }

        internal Task<ThroughputResponse> ReplaceThroughputIfExistsAsync(
            string targetRID,
            int throughput,
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ReplaceThroughputPropertiesIfExistsAsync(
                targetRID,
                ThroughputProperties.CreateManualThroughput(throughput),
                requestOptions,
                cancellationToken);
        }

        private async Task<T> GetOfferV2Async<T>(
            string targetRID,
            bool failIfNotConfigured,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(targetRID))
            {
                throw new ArgumentNullException(targetRID);
            }

            QueryDefinition queryDefinition = new QueryDefinition("select * from root r where r.offerResourceId= @targetRID");
            queryDefinition.WithParameter("@targetRID", targetRID);

            using FeedIterator<T> databaseStreamIterator = this.GetOfferQueryIterator<T>(
                queryDefinition: queryDefinition,
                continuationToken: null,
                requestOptions: null,
                cancellationToken: cancellationToken);

            T offerV2 = await this.SingleOrDefaultAsync<T>(databaseStreamIterator);

            if (offerV2 == null &&
                failIfNotConfigured)
            {
                throw CosmosExceptionFactory.CreateNotFoundException(
                    $"Throughput is not configured for {targetRID}");
            }

            return offerV2;
        }

        internal virtual FeedIterator<T> GetOfferQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (!(this.GetOfferQueryStreamIterator(
               queryDefinition,
               continuationToken,
               requestOptions,
               cancellationToken) is FeedIteratorInternal databaseStreamIterator))
            {
                throw new InvalidOperationException($"Expected a FeedIteratorInternal.");
            }

            return new FeedIteratorCore<T>(
                databaseStreamIterator,
                (response) => this.ClientContext.ResponseFactory.CreateQueryFeedResponse<T>(
                    responseMessage: response,
                    resourceType: ResourceType.Offer));
        }

        internal virtual FeedIterator GetOfferQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return new FeedIteratorCore(
               clientContext: this.ClientContext,
               resourceLink: this.OfferRootUri,
               resourceType: ResourceType.Offer,
               queryDefinition: queryDefinition,
               continuationToken: continuationToken,
               options: requestOptions);
        }

        private async Task<T> SingleOrDefaultAsync<T>(
            FeedIterator<T> offerQuery,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            while (offerQuery.HasMoreResults)
            {
                FeedResponse<T> offerFeedResponse = await offerQuery.ReadNextAsync(cancellationToken);
                if (offerFeedResponse.Any())
                {
                    return offerFeedResponse.Single();
                }
            }

            return default(T);
        }

        private async Task<ThroughputResponse> GetThroughputResponseAsync(
           Stream streamPayload,
           OperationType operationType,
           Uri linkUri,
           ResourceType resourceType,
           RequestOptions requestOptions = null,
           CancellationToken cancellationToken = default(CancellationToken))
        {
            ResponseMessage responseMessage = await this.ClientContext.ProcessResourceOperationStreamAsync(
              resourceUri: linkUri.OriginalString,
              resourceType: resourceType,
              operationType: operationType,
              cosmosContainerCore: null,
              partitionKey: null,
              streamPayload: streamPayload,
              requestOptions: requestOptions,
              requestEnricher: null,
              diagnosticsContext: null,
              cancellationToken: cancellationToken);
            return this.ClientContext.ResponseFactory.CreateThroughputResponse(responseMessage);
        }
    }
}
