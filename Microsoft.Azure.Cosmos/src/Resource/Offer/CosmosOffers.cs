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
    using Microsoft.Azure.Documents;

    internal class CosmosOffers
    {
        private readonly CosmosClientContext ClientContext;
        private readonly Uri OfferRootUri;

        public CosmosOffers(CosmosClientContext clientContext)
        {
            this.ClientContext = clientContext;
            this.OfferRootUri = new Uri(Paths.Offers_Root, UriKind.Relative);
        }

        internal async Task<ThroughputResponse> ReadThroughputAsync(
            string targetRID,
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            OfferV2 offerV2 = await this.GetOfferV2Async(targetRID, cancellationToken);

            return await this.GetThroughputResponseAsync(
                streamPayload: null,
                operationType: OperationType.Read,
                linkUri: new Uri(offerV2.SelfLink, UriKind.Relative),
                resourceType: ResourceType.Offer,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        internal async Task<ThroughputResponse> ReplaceThroughputAsync(
            string targetRID,
            int throughput,
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            OfferV2 offerV2 = await this.GetOfferV2Async(targetRID, cancellationToken);
            OfferV2 newOffer = new OfferV2(offerV2, throughput);

            return await this.GetThroughputResponseAsync(
                streamPayload: this.ClientContext.PropertiesSerializer.ToStream(newOffer),
                operationType: OperationType.Replace,
                linkUri: new Uri(offerV2.SelfLink, UriKind.Relative),
                resourceType: ResourceType.Offer,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        internal async Task<OfferV2> GetOfferV2Async(
            string targetRID,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(targetRID))
            {
                throw new ArgumentNullException(targetRID);
            }

            QueryDefinition queryDefinition = new QueryDefinition("select * from root r where r.offerResourceId= @targetRID");
            queryDefinition.WithParameter("@targetRID", targetRID);

            FeedIterator<OfferV2> databaseStreamIterator = this.GetOfferQueryIterator<OfferV2>(
                 queryDefinition);
            OfferV2 offerV2 = await this.SingleOrDefaultAsync<OfferV2>(databaseStreamIterator);

            if (offerV2 == null)
            {
                throw new CosmosException(HttpStatusCode.NotFound, "Throughput is not configured");
            }

            return offerV2;
        }

        internal virtual FeedIterator<T> GetOfferQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            FeedIterator databaseStreamIterator = GetOfferQueryStreamIterator(
               queryDefinition,
               continuationToken,
               requestOptions,
               cancellationToken);

            return new FeedIteratorCore<T>(
                databaseStreamIterator,
                this.ClientContext.ResponseFactory.CreateQueryFeedResponse<T>);
        }

        internal virtual FeedIterator GetOfferQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return new FeedIteratorCore(
               this.ClientContext,
               this.OfferRootUri,
               ResourceType.Offer,
               queryDefinition,
               continuationToken,
               requestOptions);
        }

        private CosmosOfferResult GetThroughputIfExists(Offer offer)
        {
            if (offer == null)
            {
                return new CosmosOfferResult(null);
            }

            OfferV2 offerV2 = offer as OfferV2;
            if (offerV2 == null)
            {
                throw new NotImplementedException();
            }

            return new CosmosOfferResult(offerV2.Content.OfferThroughput);
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
            Task<ResponseMessage> responseMessage = this.ClientContext.ProcessResourceOperationStreamAsync(
              resourceUri: linkUri,
              resourceType: resourceType,
              operationType: operationType,
              cosmosContainerCore: null,
              partitionKey: null,
              streamPayload: streamPayload,
              requestOptions: requestOptions,
              requestEnricher: null,
              cancellationToken: cancellationToken);
            return await this.ClientContext.ResponseFactory.CreateThroughputResponseAsync(responseMessage);
        }

    }
}
