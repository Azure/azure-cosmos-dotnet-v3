//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.SkipTake
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Newtonsoft.Json;

    internal abstract partial class TakeDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private sealed class ClientTakeDocumentQueryExecutionComponent : TakeDocumentQueryExecutionComponent
        {
            private ClientTakeDocumentQueryExecutionComponent(IDocumentQueryExecutionComponent source, int takeCount, TakeEnum takeEnum)
                : base(source, takeCount, takeEnum)
            {
                // Work is done in the base class.
            }

            public static async Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateLimitDocumentQueryExecutionComponentAsync(
                int limitCount,
                RequestContinuationToken requestContinuationToken,
                Func<RequestContinuationToken, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync)
            {
                if (tryCreateSourceAsync == null)
                {
                    throw new ArgumentNullException(nameof(tryCreateSourceAsync));
                }

                LimitContinuationToken limitContinuationToken;
                if (requestContinuationToken.IsNull)
                {
                    if (!LimitContinuationToken.TryParse(requestContinuationToken, out limitContinuationToken))
                    {
                        return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                            new MalformedContinuationTokenException($"Malformed {nameof(LimitContinuationToken)}: {requestContinuationToken}."));
                    }
                }
                else
                {
                    limitContinuationToken = new LimitContinuationToken(limitCount, null);
                }

                if (limitContinuationToken.Limit > limitCount)
                {
                    return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                        new MalformedContinuationTokenException($"{nameof(LimitContinuationToken.Limit)} in {nameof(LimitContinuationToken)}: {continuationToken}: {limitContinuationToken.Limit} can not be greater than the limit count in the query: {limitCount}."));
                }

                if (limitCount < 0)
                {
                    return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                            new MalformedContinuationTokenException($"{nameof(limitCount)}: {limitCount} must be a non negative number."));
                }

                return (await tryCreateSourceAsync(limitContinuationToken.SourceToken))
                    .Try<IDocumentQueryExecutionComponent>((source) => new TakeDocumentQueryExecutionComponent(
                    source,
                    limitContinuationToken.Limit,
                    TakeEnum.Limit));
            }

            public static async Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateTopDocumentQueryExecutionComponentAsync(
                int topCount,
                string continuationToken,
                Func<string, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync)
            {
                if (tryCreateSourceAsync == null)
                {
                    throw new ArgumentNullException(nameof(tryCreateSourceAsync));
                }

                TopContinuationToken topContinuationToken;
                if (continuationToken != null)
                {
                    if (!TopContinuationToken.TryParse(continuationToken, out topContinuationToken))
                    {
                        return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                            new MalformedContinuationTokenException($"Malformed {nameof(LimitContinuationToken)}: {continuationToken}."));
                    }
                }
                else
                {
                    topContinuationToken = new TopContinuationToken(topCount, null);
                }

                if (topContinuationToken.Top > topCount)
                {
                    return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                        new MalformedContinuationTokenException($"{nameof(TopContinuationToken.Top)} in {nameof(TopContinuationToken)}: {continuationToken}: {topContinuationToken.Top} can not be greater than the top count in the query: {topCount}."));
                }

                if (topCount < 0)
                {
                    return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                            new MalformedContinuationTokenException($"{nameof(topCount)}: {topCount} must be a non negative number."));
                }

                return (await tryCreateSourceAsync(topContinuationToken.SourceToken))
                    .Try<IDocumentQueryExecutionComponent>((source) => new TakeDocumentQueryExecutionComponent(
                    source,
                    topContinuationToken.Top,
                    TakeEnum.Top));
            }
        }
    }
}