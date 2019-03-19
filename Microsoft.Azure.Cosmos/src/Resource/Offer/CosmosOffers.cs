//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Linq;
    using Newtonsoft.Json.Linq;

    internal class CosmosOffers
    {
        private readonly IDocumentClient documentClient;

        public CosmosOffers(IDocumentClient documentClient)
        {
            this.documentClient = documentClient;
        }

        internal CosmosOfferResult ReadProvisionedThroughputIfExistsAsync(
            string targetRID,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(targetRID))
            {
                throw new ArgumentNullException(targetRID);
            }

            try
            {
                Offer offer = this.ReadOfferAsync(targetRID, cancellationToken);
                return this.GetThroughputIfExists(offer);
            }
            catch (DocumentClientException dce)
            {
                return new CosmosOfferResult(
                    dce.StatusCode ?? HttpStatusCode.InternalServerError,
                    new CosmosException(
                        dce.Message?.Replace(Environment.NewLine, string.Empty),
                        dce.StatusCode ?? HttpStatusCode.InternalServerError,
                        (int)dce.GetSubStatus(),
                        dce.ActivityId,
                        dce.RequestCharge));
            }
            catch (AggregateException ex)
            {
                CosmosOfferResult offerResult = CosmosOffers.TryToOfferResult(ex);
                if (offerResult != null)
                {
                    return offerResult;
                }

                throw;
            }
        }

        internal async Task<CosmosOfferResult> ReplaceThroughputIfExistsAsync(
            string targetRID,
            int targetThroughput,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                Offer offer = this.ReadOfferAsync(targetRID, cancellationToken);
                if (offer == null)
                {
                    throw new ArgumentOutOfRangeException("Throughput is not configured");
                }

                OfferV2 offerV2 = offer as OfferV2;
                if (offerV2 == null)
                {
                    throw new NotImplementedException();
                }

                OfferV2 newOffer = new OfferV2(offerV2, targetThroughput);
                Offer replacedOffer = await this.ReplaceOfferAsync(targetRID, newOffer, cancellationToken);
                offerV2 = replacedOffer as OfferV2;
                Debug.Assert(offerV2 != null);

                return new CosmosOfferResult(offerV2.Content.OfferThroughput);
            }
            catch (DocumentClientException dce)
            {
                return new CosmosOfferResult(
                    dce.StatusCode ?? HttpStatusCode.InternalServerError,
                    new CosmosException(
                        dce.Message?.Replace(Environment.NewLine, string.Empty),
                        dce.StatusCode ?? HttpStatusCode.InternalServerError,
                        (int)dce.GetSubStatus(),
                        dce.ActivityId,
                        dce.RequestCharge));
            }
            catch (AggregateException ex)
            {
                CosmosOfferResult offerResult = CosmosOffers.TryToOfferResult(ex);
                if (offerResult != null)
                {
                    return offerResult;
                }

                throw;
            }
        }

        private static CosmosOfferResult TryToOfferResult(AggregateException ex)
        {
            AggregateException innerExceptions = ex.Flatten();
            DocumentClientException dce = innerExceptions.InnerExceptions.FirstOrDefault(innerEx => innerEx is DocumentClientException) as DocumentClientException;
            if (dce != null)
            {
                return new CosmosOfferResult(
                    dce.StatusCode ?? HttpStatusCode.InternalServerError,
                    new CosmosException(
                        dce.Message?.Replace(Environment.NewLine, string.Empty),
                        dce.StatusCode ?? HttpStatusCode.InternalServerError,
                        (int)dce.GetSubStatus(),
                        dce.ActivityId,
                        dce.RequestCharge));
            }

            return null;
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

        private Offer ReadOfferAsync(string targetRID,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(targetRID))
            {
                throw new ArgumentNullException(nameof(targetRID));
            }

            SqlQuerySpec spec = new SqlQuerySpec("SELECT * FROM offers o WHERE o.offerResourceId = @targetRID ",
                new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@targetRID", Value = targetRID } }));

            JObject offer = this.documentClient.CreateOfferQuery(spec).AsEnumerable().FirstOrDefault();
            if(offer != null)
            {
                return offer.ToObject<OfferV2>();
            }

            return null;
        }

        private Task<T> SingleOrDefaultAsync<T>(
            IDocumentQuery<T> offerQuery,
            CancellationToken cancellationToken)
        {
            if (offerQuery.HasMoreResults)
            {
                return offerQuery.ExecuteNextAsync<T>(cancellationToken)
                    .ContinueWith(nextAsyncTask =>
                    {
                        FeedResponse<T> offerFeedResponse = nextAsyncTask.Result;
                        if (offerFeedResponse.Any())
                        {
                            return Task.FromResult(offerFeedResponse.Single());
                        }

                        return SingleOrDefaultAsync(offerQuery, cancellationToken);
                    })
                    .Unwrap();
            }

            return Task.FromResult(default(T));
        }

        private Task<Offer> ReplaceOfferAsync(
                        string targetRID,
                        Offer targetOffer,
                        CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(targetRID))
            {
                throw new ArgumentNullException(nameof(targetRID));
            }

            if (targetOffer == null)
            {
                throw new ArgumentNullException(nameof(targetOffer));
            }

            if (!string.Equals(targetRID, targetOffer.OfferResourceId, StringComparison.Ordinal))
            {
                throw new ArgumentOutOfRangeException($"Offer imcompatible with resoruce RID {targetRID}");
            }

            return this.documentClient.ReplaceOfferAsync(targetOffer)
                .ContinueWith(task => task.Result.Resource);
        }
    }
}
