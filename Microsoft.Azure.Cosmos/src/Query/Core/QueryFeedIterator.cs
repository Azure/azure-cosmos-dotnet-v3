// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal sealed class QueryFeedIterator : IDisposable
    {
        private readonly FeedIteratorInternal feedIterator;

        public QueryFeedIterator(FeedIteratorInternal feedIterator)
        {
            this.feedIterator = feedIterator ?? throw new ArgumentNullException(nameof(feedIterator));
        }

        public bool HasMoreResults => this.feedIterator.HasMoreResults;

        public async Task<Response> ReadNextPageAsync(CancellationToken cancellationToken = default)
        {
            ResponseMessage responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken);
            if (!(responseMessage is QueryResponse queryResponse))
            {
                throw new InvalidOperationException("Expected a query response.");
            }

            if (queryResponse.StatusCode == HttpStatusCode.OK)
            {
                return new SuccessResponse(
                    queryResponse.CosmosElements,
                    queryResponse.Headers.RequestCharge);
            }

            if ((int)queryResponse.StatusCode == 429)
            {
                return new ThrottledResponse(queryResponse.Headers.RetryAfter.Value);
            }

            return new GenericFailureResponse(queryResponse.StatusCode, queryResponse.ErrorMessage);
        }

        public CosmosElement ContinuationToken() => this.feedIterator.GetCosmosElementContinuationToken();

        public void Dispose()
        {
            this.feedIterator.Dispose();
        }

        public abstract class Response
        {
        }

        public sealed class SuccessResponse : Response
        {
            public SuccessResponse(
                IReadOnlyList<CosmosElement> documents,
                double requestCharge)
            {
                this.Documents = documents ?? throw new ArgumentNullException(nameof(documents));
                this.RequestCharge = requestCharge >= 0 ? requestCharge : throw new ArgumentOutOfRangeException(nameof(requestCharge));
            }

            public IReadOnlyList<CosmosElement> Documents { get; }
            public double RequestCharge { get; }
        }

        public abstract class FailureResponse : Response
        {
            protected FailureResponse(HttpStatusCode httpStatusCode, string message)
            {
                this.HttpStatusCode = httpStatusCode;
                this.Message = message ?? throw new ArgumentNullException(nameof(message));
            }

            public HttpStatusCode HttpStatusCode { get; }
            public string Message { get; }
        }

        public sealed class GenericFailureResponse : FailureResponse
        {
            public GenericFailureResponse(HttpStatusCode httpStatusCode, string message)
                : base(httpStatusCode, message)
            {
            }
        }

        public sealed class ThrottledResponse : FailureResponse
        {
            public ThrottledResponse(TimeSpan retryAfter)
                : base((HttpStatusCode)429, message: "Request Rate Too Large")
            {
                this.RetryAfter = retryAfter;
            }

            public TimeSpan RetryAfter { get; }
        }
    }
}
