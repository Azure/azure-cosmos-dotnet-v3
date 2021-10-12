namespace Queries
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;

    public class FixedWindowQueryIterator<T> : FeedIterator<T>
    {
        private readonly int windowSize;

        private readonly FeedIterator<T> feedIterator;

        private ContinuationState state;

        private FixedWindowQueryIterator(int windowSize, FeedIterator<T> feedIterator, ContinuationState state)
        {
            this.windowSize = windowSize;
            this.feedIterator = feedIterator;
            this.state = state;
        }

        public override bool HasMoreResults => this.state.HasMoreResults;


        public static FeedIterator<T> Create(
            Container container,
            QueryDefinition query,
            QueryRequestOptions requestOptions,
            string outerContinuationtoken,
            int windowSize)
        {
            ContinuationState state = outerContinuationtoken != null
                ? ContinuationState.Create(outerContinuationtoken)
                : new ContinuationState(true);
            FeedIterator<T> feedIterator = container.GetItemQueryIterator<T>(query, state.ContinuationToken, requestOptions);
            return new FixedWindowQueryIterator<T>(windowSize, feedIterator, state);
        }

        public override async Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            if (!this.HasMoreResults)
            {
                throw new InvalidOperationException("attempting to drain an empty iterator");
            }

            FeedResponse<T> feedResponse = null;
            List<T> accumulator = new List<T>();
            for (int skipCount = this.state.SkipCount; skipCount > 0 && this.feedIterator.HasMoreResults;)
            {
                feedResponse = await this.feedIterator.ReadNextAsync();
                skipCount -= feedResponse.Count;
            }

            int taken = 0;
            while (this.windowSize - accumulator.Count > 0 && this.feedIterator.HasMoreResults)
            {
                feedResponse = await this.feedIterator.ReadNextAsync();
                taken = Math.Min(feedResponse.Count, this.windowSize - accumulator.Count);
                accumulator.AddRange(feedResponse.Take(taken));
            }

            bool hasMoreResults = (accumulator.Count == this.windowSize) && this.feedIterator.HasMoreResults;
            this.state = hasMoreResults ?
                new ContinuationState(token: feedResponse.ContinuationToken, skip: taken) :
                new ContinuationState(hasMoreResults);

            return accumulator.Count == 0 ?
                new EmptyFeedResponse(feedResponse?.Headers, feedResponse?.Diagnostics):
                new FixedWindowFeedResponse(
                    resource: accumulator,
                    continuationToken: hasMoreResults ? this.state.ToString() : null,
                    indexMetrics: feedResponse.IndexMetrics,
                    headers: feedResponse.Headers,
                    statusCode: feedResponse.StatusCode,
                    diagnostics: feedResponse.Diagnostics);
        }

        private class FixedWindowFeedResponse : FeedResponse<T>
        {
            private readonly IReadOnlyList<T> resource;

            public FixedWindowFeedResponse(
                IReadOnlyList<T> resource,
                string continuationToken,
                string indexMetrics,
                Headers headers,
                HttpStatusCode statusCode,
                CosmosDiagnostics diagnostics)
            {
                this.resource = resource;
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

            public override HttpStatusCode StatusCode { get; }

            public override CosmosDiagnostics Diagnostics { get; }

            public override IEnumerator<T> GetEnumerator()
            {
                return this.resource.GetEnumerator();
            }
        }

        private class EmptyFeedResponse : FeedResponse<T>
        {
            public EmptyFeedResponse(Headers headers, CosmosDiagnostics diagnostics)
            {
                this.Headers = headers;
                this.Diagnostics = diagnostics;
            }

            public override string ContinuationToken => null;

            public override int Count => 0;

            public override string IndexMetrics => string.Empty;

            public override Headers Headers { get; }

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
            {
                this.ContinuationToken = token;
                this.SkipCount = skip;
                this.HasMoreResults = true;
            }

            public ContinuationState(bool hasMoreResults)
            {
                this.ContinuationToken = null;
                this.SkipCount = 0;
                this.HasMoreResults = hasMoreResults;
            }

            public string ContinuationToken { get; }

            public int SkipCount { get; }

            [JsonIgnore]
            public bool HasMoreResults { get; }

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
