//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.ExecutionComponent
{
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections.Generic;
    using Microsoft.Azure.Cosmos.Internal;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class TopDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private int topCount;

        private TopDocumentQueryExecutionComponent(IDocumentQueryExecutionComponent source, int topCount)
            : base(source)
        {
            this.topCount = topCount;
        }

        public static async Task<TopDocumentQueryExecutionComponent> CreateAsync(
            int topCount,
            string requestContinuation,
            Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback)
        {
            TopContinuationToken continuationToken = null;
            try
            {
                if (!string.IsNullOrEmpty(requestContinuation))
                {
                    continuationToken = JsonConvert.DeserializeObject<TopContinuationToken>(requestContinuation);
                }
            }
            catch (JsonException ex)
            {
                DefaultTrace.TraceWarning(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} Invalid continuation token {1} for Top~Component, exception: {2}",
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    requestContinuation,
                    ex.Message));

                throw new BadRequestException("Invalid continuation token", ex);
            }

            string sourceContinuation = null;
            if (continuationToken != null)
            {
                if (continuationToken.Top <= 0 || continuationToken.Top > topCount)
                {
                    throw new BadRequestException("Invalid top in continuation token");
                }

                topCount = continuationToken.Top;
                if (continuationToken.SourceToken != null)
                {
                    sourceContinuation = (string)continuationToken.SourceToken.Value;
                }
            }

            return new TopDocumentQueryExecutionComponent(await createSourceCallback(sourceContinuation), topCount);
        }

        public override bool IsDone
        {
            get
            {
                return this.source.IsDone || this.topCount <= 0;
            }
        }

        public override async Task<FeedResponse<object>> DrainAsync(int maxElements, CancellationToken token)
        {
            FeedResponse<object> result = await base.DrainAsync(maxElements, token);
            if (result.Count > this.topCount)
            {
                result = new FeedResponse<object>(
                    new CountableEnumerable<object>(result, this.topCount),
                    this.topCount,
                    result.Headers,
                    result.UseETagAsContinuation,
                    result.QueryMetrics,
                    result.RequestStatistics,
                    result.DisallowContinuationTokenMessage,
                    result.ResponseLengthBytes);
            }

            this.topCount -= result.Count;

            if (this.topCount <= 0)
            {
                this.source.Stop();
            }

            if (result.DisallowContinuationTokenMessage == null)
            {
                if (!this.IsDone)
                {
                    string sourceContinuation = result.ResponseContinuation;
                    result.ResponseContinuation = JsonConvert.SerializeObject(new TopContinuationToken
                    {
                        Top = this.topCount,
                        SourceToken = sourceContinuation == null ? null : new JRaw(sourceContinuation),
                    });
                }
                else
                {
                    result.ResponseContinuation = null;
                }
            }

            return result;
        }

        private sealed class TopContinuationToken
        {
            [JsonProperty("top")]
            public int Top
            {
                get;
                set;
            }

            [JsonProperty("sourceToken")]
            public JRaw SourceToken
            {
                get;
                set;
            }
        }
    }
}