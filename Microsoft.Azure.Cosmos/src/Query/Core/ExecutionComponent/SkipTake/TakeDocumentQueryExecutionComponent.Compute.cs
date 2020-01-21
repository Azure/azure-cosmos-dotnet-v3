//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.SkipTake
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal abstract partial class TakeDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private sealed class ComputeTakeDocumentQueryExecutionComponent : TakeDocumentQueryExecutionComponent
        {
            private const string SourceTokenName = "SourceToken";
            private const string TakeCountName = "TakeCount";

            private ComputeTakeDocumentQueryExecutionComponent(IDocumentQueryExecutionComponent source, int takeCount)
                : base(source, takeCount)
            {
                // Work is done in the base class.
            }

            public static async Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateAsync(
                int takeCount,
                RequestContinuationToken requestContinuationToken,
                Func<RequestContinuationToken, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync)
            {
                if (takeCount < 0)
                {
                    throw new ArgumentException($"{nameof(takeCount)}: {takeCount} must be a non negative number.");
                }

                if (tryCreateSourceAsync == null)
                {
                    throw new ArgumentNullException(nameof(tryCreateSourceAsync));
                }

                if (requestContinuationToken == null)
                {
                    throw new ArgumentNullException(nameof(requestContinuationToken));
                }

                if (!(requestContinuationToken is CosmosElementRequestContinuationToken cosmosElementRequestContinuationToken))
                {
                    throw new ArgumentException($"Unknown {nameof(RequestContinuationToken)} type: {requestContinuationToken.GetType()}.");
                }

                TakeContinuationToken takeContinuationToken;
                if (!requestContinuationToken.IsNull)
                {
                    if (!TakeContinuationToken.TryParse(cosmosElementRequestContinuationToken.Value, out takeContinuationToken))
                    {
                        return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                            new MalformedContinuationTokenException($"Malformed {nameof(TakeContinuationToken)}: {cosmosElementRequestContinuationToken.Value}."));
                    }
                }
                else
                {
                    takeContinuationToken = new TakeContinuationToken(takeCount, sourceToken: null);
                }

                if (takeContinuationToken.TakeCount > takeCount)
                {
                    return TryCatch<IDocumentQueryExecutionComponent>.FromException(
                        new MalformedContinuationTokenException($"{nameof(TakeContinuationToken.TakeCount)} in {nameof(TakeContinuationToken)}: {cosmosElementRequestContinuationToken.Value}: {takeContinuationToken.TakeCount} can not be greater than the limit count in the query: {takeCount}."));
                }

                return (await tryCreateSourceAsync(RequestContinuationToken.Create(takeContinuationToken.SourceToken)))
                    .Try<IDocumentQueryExecutionComponent>((source) => new ComputeTakeDocumentQueryExecutionComponent(
                    source,
                    takeContinuationToken.TakeCount));
            }

            public override void SerializeState(IJsonWriter jsonWriter)
            {
                if (jsonWriter == null)
                {
                    throw new ArgumentNullException(nameof(jsonWriter));
                }

                if (!this.IsDone)
                {
                    jsonWriter.WriteObjectStart();
                    jsonWriter.WriteFieldName(SourceTokenName);
                    this.Source.SerializeState(jsonWriter);
                    jsonWriter.WriteFieldName(TakeCountName);
                    jsonWriter.WriteInt64Value(this.takeCount);
                    jsonWriter.WriteObjectEnd();
                }
            }

            private readonly struct TakeContinuationToken
            {
                public TakeContinuationToken(long takeCount, CosmosElement sourceToken)
                {
                    if ((takeCount < 0) || (takeCount > int.MaxValue))
                    {
                        throw new ArgumentException($"{nameof(takeCount)} must be a non negative number.");
                    }

                    this.TakeCount = (int)takeCount;
                    this.SourceToken = sourceToken;
                }

                public int TakeCount { get; }

                public CosmosElement SourceToken { get; }

                public static bool TryParse(CosmosElement value, out TakeContinuationToken takeContinuationToken)
                {
                    if (value == null)
                    {
                        throw new ArgumentNullException(nameof(value));
                    }

                    if (!(value is CosmosObject continuationToken))
                    {
                        takeContinuationToken = default;
                        return false;
                    }

                    if (!continuationToken.TryGetValue(TakeCountName, out CosmosNumber takeCount))
                    {
                        takeContinuationToken = default;
                        return false;
                    }

                    if (!continuationToken.TryGetValue(SourceTokenName, out CosmosElement sourceToken))
                    {
                        takeContinuationToken = default;
                        return false;
                    }

                    takeContinuationToken = new TakeContinuationToken(takeCount.AsInteger().Value, sourceToken);
                    return true;
                }
            }
        }
    }
}