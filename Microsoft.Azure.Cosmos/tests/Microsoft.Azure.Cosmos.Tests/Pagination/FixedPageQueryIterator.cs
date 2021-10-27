//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;

    public class FixedPageQueryIterator<T> : FeedIterator<T>
    {
        private readonly static ContinuationState Done = new ContinuationState(
            token: null,
            skip: 0,
            hasMoreResults: false,
            cachedResponse: null);

        private readonly int fixedPageSize;

        private readonly FeedIterator<T> feedIterator;

        private ContinuationState state;

        private FixedPageQueryIterator(int fixedPageSize, FeedIterator<T> feedIterator, ContinuationState state)
        {
            this.fixedPageSize = fixedPageSize;
            this.feedIterator = feedIterator;
            this.state = state;
        }

        public override bool HasMoreResults => this.state.HasMoreResults;


        public static FeedIterator<T> Create(
            Container container,
            QueryDefinition query,
            QueryRequestOptions requestOptions,
            string outerContinuationtoken,
            int fixedPageSize)
        {
            ContinuationState state = outerContinuationtoken != null
                ? ContinuationState.Create(outerContinuationtoken)
                : new ContinuationState(skip: 0, token: null);
            FeedIterator<T> feedIterator = container.GetItemQueryIterator<T>(query, state.ContinuationToken, requestOptions);
            return new FixedPageQueryIterator<T>(fixedPageSize, feedIterator, state);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.feedIterator.Dispose();
            }
        }

        public override async Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            if (!this.HasMoreResults)
            {
                throw new InvalidOperationException("attempting to drain an empty iterator");
            }

            ResponseAccumulator accumulator = ResponseAccumulator.Create();
            int skipCount = this.state.SkipCount;
            FeedResponse<T> feedResponse = this.state.CachedResponse ??
                (this.feedIterator.HasMoreResults ? await this.feedIterator.ReadNextAsync() : null);

            while (skipCount > 0 && feedResponse != null && feedResponse.Count < skipCount)
            {
                accumulator.Add(feedResponse.RequestCharge, feedResponse.IndexMetrics);
                skipCount -= feedResponse.Count;
                feedResponse = this.feedIterator.HasMoreResults ? await this.feedIterator.ReadNextAsync() : null;
            }

            int taken = 0;
            if (feedResponse != null)
            {
                Debug.Assert(feedResponse.Count >= skipCount);
                accumulator.Add(feedResponse.Skip(skipCount).Take(this.fixedPageSize));
                taken = skipCount + accumulator.Resource.Count;
            }

            string continuationToken = feedResponse?.ContinuationToken;
            while (this.fixedPageSize - accumulator.Resource.Count > 0 && this.feedIterator.HasMoreResults)
            {
                continuationToken = feedResponse?.ContinuationToken;
                feedResponse = await this.feedIterator.ReadNextAsync();
                taken = Math.Min(feedResponse.Count, this.fixedPageSize - accumulator.Resource.Count);
                accumulator.Add(feedResponse.Take(taken), feedResponse.RequestCharge, feedResponse.IndexMetrics);
            }

            bool hasMoreResults = (accumulator.Resource.Count == this.fixedPageSize) && (this.feedIterator.HasMoreResults || feedResponse.Count > taken);
            this.state = hasMoreResults ?
                new ContinuationState(token: continuationToken, skip: taken, hasMoreResults: true, cachedResponse: feedResponse) :
                Done;

            return accumulator.Resource.Count == 0 ?
                new EmptyFeedResponse(feedResponse?.Headers, feedResponse?.Diagnostics, accumulator.RequestCharge, accumulator.IndexMetrics):
                new FixedPageQueryFeedResponse(
                    resource: accumulator.Resource,
                    requestCharge: accumulator.RequestCharge,
                    continuationToken: hasMoreResults ? this.state.ToString() : null,
                    indexMetrics: accumulator.IndexMetrics,
                    headers: feedResponse.Headers,
                    statusCode: feedResponse.StatusCode,
                    diagnostics: feedResponse.Diagnostics);
        }

        private struct ResponseAccumulator
        {
            private readonly StringBuilder indexMetricsBuilder;

            private readonly List<T> resource;

            public ResponseAccumulator(StringBuilder indexMetricsBuilder, List<T> resource) : this()
            {
                this.indexMetricsBuilder = indexMetricsBuilder;
                this.resource = resource;
            }

            public double RequestCharge { get; private set; }

            public IReadOnlyList<T> Resource => this.resource;

            public string IndexMetrics => this.indexMetricsBuilder.ToString();

            // Implementing Diagnostics Accumulator left as future improvement
            // public CosmosDiagnostics Diagnostics { get; private set; }

            public static ResponseAccumulator Create()
            {
                ResponseAccumulator accumulator = new ResponseAccumulator(new StringBuilder(), new List<T>());
                return accumulator;
            }

            public void Add(IEnumerable<T> resource, double requestCharge, string indexMetrics)
            {
                this.RequestCharge += requestCharge;
                this.indexMetricsBuilder.Append(indexMetrics);
                this.indexMetricsBuilder.AppendLine();
                this.resource.AddRange(resource);
            }

            public void Add(double requestCharge, string indexMetrics)
            {
                this.RequestCharge += requestCharge;
                this.indexMetricsBuilder.Append(indexMetrics);
                this.indexMetricsBuilder.AppendLine();
            }

            public void Add(IEnumerable<T> resource)
            {
                this.resource.AddRange(resource);
            }
        }

        private class FixedPageQueryFeedResponse : FeedResponse<T>
        {
            private readonly IReadOnlyList<T> resource;

            private readonly double requestCharge;

            public FixedPageQueryFeedResponse(
                IReadOnlyList<T> resource,
                double requestCharge,
                string continuationToken,
                string indexMetrics,
                Headers headers,
                HttpStatusCode statusCode,
                CosmosDiagnostics diagnostics)
            {
                this.resource = resource;
                this.requestCharge = requestCharge;
                this.ContinuationToken = continuationToken;
                this.IndexMetrics = indexMetrics;
                this.Headers = headers;
                this.StatusCode = statusCode;
                this.Diagnostics = diagnostics;
            }

            public override string ContinuationToken { get; }

            public override int Count => this.resource.Count;

            public override string IndexMetrics { get; }

            public override Headers Headers { get; }

            public override IEnumerable<T> Resource => this.resource;

            public override double RequestCharge => this.requestCharge;

            public override HttpStatusCode StatusCode { get; }

            public override CosmosDiagnostics Diagnostics { get; }

            public override IEnumerator<T> GetEnumerator()
            {
                return this.resource.GetEnumerator();
            }
        }

        private class EmptyFeedResponse : FeedResponse<T>
        {
            private readonly double requestCharge;

            private readonly string indexMetrics;

            public EmptyFeedResponse(Headers headers, CosmosDiagnostics diagnostics, double requestCharge, string indexMetrics)
            {
                this.Headers = headers;
                this.Diagnostics = diagnostics;
                this.requestCharge = requestCharge;
                this.indexMetrics = indexMetrics;
            }

            public override string ContinuationToken => null;

            public override int Count => 0;

            public override string IndexMetrics => this.indexMetrics;

            public override Headers Headers { get; }

            public override double RequestCharge => this.requestCharge;

            public override IEnumerable<T> Resource => Enumerable.Empty<T>();

            public override HttpStatusCode StatusCode => HttpStatusCode.OK;

            public override CosmosDiagnostics Diagnostics { get; }

            public override IEnumerator<T> GetEnumerator()
            {
                return Enumerable.Empty<T>().GetEnumerator();
            }
        }

        private readonly struct ContinuationState
        {
            [JsonConstructor]
            public ContinuationState(string token, int skip)
                : this(token: token, skip: skip, hasMoreResults: true, cachedResponse: null)
            {
            }

            public ContinuationState(
                string token,
                int skip,
                bool hasMoreResults,
                FeedResponse<T> cachedResponse)
            {
                this.ContinuationToken = token;
                this.CachedResponse = cachedResponse;
                this.HasMoreResults = hasMoreResults;
                this.SkipCount = skip;
            }

            public string ContinuationToken { get; }

            public int SkipCount { get; }

            [JsonIgnore]
            public bool HasMoreResults { get; }

            [JsonIgnore]
            public FeedResponse<T> CachedResponse { get; }

            public override string ToString()
            {
                return  this.HasMoreResults ? JsonConvert.SerializeObject(this) : null;
            }

            public static ContinuationState Create(string continuationToken)
            {
                if (continuationToken == null)
                {
                    throw new ArgumentNullException(nameof(continuationToken));
                }

                ContinuationState state = JsonConvert.DeserializeObject<ContinuationState>(continuationToken);
                return state;
            }
        }
    }
}
