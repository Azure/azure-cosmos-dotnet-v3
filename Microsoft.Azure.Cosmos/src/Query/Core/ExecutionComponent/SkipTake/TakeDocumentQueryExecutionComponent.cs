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
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Newtonsoft.Json;

    internal abstract partial class TakeDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private int takeCount;

        protected TakeDocumentQueryExecutionComponent(
            IDocumentQueryExecutionComponent source,
            int takeCount)
            : base(source)
        {
            this.takeCount = takeCount;
        }

        public static Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateLimitDocumentQueryExecutionComponentAsync(
            ExecutionEnvironment executionEnvironment,
            int limitCount,
            RequestContinuationToken requestContinuationToken,
            Func<RequestContinuationToken, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync)
        {
            Task<TryCatch<IDocumentQueryExecutionComponent>> tryCreateComponentAsync;
            switch (executionEnvironment)
            {
                case ExecutionEnvironment.Client:
                    tryCreateComponentAsync = ClientTakeDocumentQueryExecutionComponent.TryCreateLimitDocumentQueryExecutionComponentAsync(
                        limitCount,
                        requestContinuationToken,
                        tryCreateSourceAsync);
                    break;

                case ExecutionEnvironment.Compute:
                    tryCreateComponentAsync = ComputeTakeDocumentQueryExecutionComponent.TryCreateAsync(
                        limitCount,
                        requestContinuationToken,
                        tryCreateSourceAsync);
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(ExecutionEnvironment)}: {executionEnvironment}.");
            }

            return tryCreateComponentAsync;
        }

        public static Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateTopDocumentQueryExecutionComponentAsync(
            ExecutionEnvironment executionEnvironment,
            int topCount,
            RequestContinuationToken requestContinuationToken,
            Func<RequestContinuationToken, Task<TryCatch<IDocumentQueryExecutionComponent>>> tryCreateSourceAsync)
        {
            Task<TryCatch<IDocumentQueryExecutionComponent>> tryCreateComponentAsync;
            switch (executionEnvironment)
            {
                case ExecutionEnvironment.Client:
                    tryCreateComponentAsync = ClientTakeDocumentQueryExecutionComponent.TryCreateTopDocumentQueryExecutionComponentAsync(
                        topCount,
                        requestContinuationToken,
                        tryCreateSourceAsync);
                    break;

                case ExecutionEnvironment.Compute:
                    tryCreateComponentAsync = ComputeTakeDocumentQueryExecutionComponent.TryCreateAsync(
                        topCount,
                        requestContinuationToken,
                        tryCreateSourceAsync);
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(ExecutionEnvironment)}: {executionEnvironment}.");
            }

            return tryCreateComponentAsync;
        }

        public override bool IsDone
        {
            get
            {
                return this.Source.IsDone || this.takeCount <= 0;
            }
        }

        public override async Task<QueryResponseCore> DrainAsync(int maxElements, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            QueryResponseCore results = await base.DrainAsync(maxElements, token);
            if (!results.IsSuccess)
            {
                return results;
            }

            List<CosmosElement> takedDocuments = results.CosmosElements.Take(this.takeCount).ToList();
            this.takeCount -= takedDocuments.Count;

            string updatedContinuationToken = null;
            if (results.DisallowContinuationTokenMessage == null)
            {
                IJsonWriter jsonWriter = Json.JsonWriter.Create(JsonSerializationFormat.Text);
                this.SerializeState(jsonWriter);
                updatedContinuationToken = Utf8StringHelpers.ToString(jsonWriter.GetResult());
            }

            return QueryResponseCore.CreateSuccess(
                    result: takedDocuments,
                    continuationToken: updatedContinuationToken,
                    disallowContinuationTokenMessage: results.DisallowContinuationTokenMessage,
                    activityId: results.ActivityId,
                    requestCharge: results.RequestCharge,
                    diagnostics: results.Diagnostics,
                    responseLengthBytes: results.ResponseLengthBytes);
        }
    }
}