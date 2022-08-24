//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.SingleRoundtripOptimisticExecutionQuery
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;

    /// <summary>
    /// A continuation token that has both backend continuation token and partition range information. 
    /// </summary>
    internal sealed class SingleRoundtripOptimisticExecutionContinuationToken : IPartitionedToken
    {
        private static readonly string singleRoundtripOptimisticExec = "singleRoundtripOptimisticExec";

        public SingleRoundtripOptimisticExecutionContinuationToken(ParallelContinuationToken token)
        {
            this.Token = token;
        }

        public ParallelContinuationToken Token { get; }

        public Range<string> Range => this.Token.Range;

        public static CosmosElement ToCosmosElement(SingleRoundtripOptimisticExecutionContinuationToken continuationToken)
        {
            CosmosElement inner = ParallelContinuationToken.ToCosmosElement(continuationToken.Token);
            return CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                [singleRoundtripOptimisticExec] = inner
                            });
        }

        public static TryCatch<SingleRoundtripOptimisticExecutionContinuationToken> TryCreateFromCosmosElement(CosmosElement cosmosElement)
        {
            CosmosObject cosmosObjectContinuationToken = (CosmosObject)cosmosElement;
            TryCatch<ParallelContinuationToken> inner = ParallelContinuationToken.TryCreateFromCosmosElement(cosmosObjectContinuationToken[singleRoundtripOptimisticExec]);

            return inner.Succeeded ?
                TryCatch<SingleRoundtripOptimisticExecutionContinuationToken>.FromResult(new SingleRoundtripOptimisticExecutionContinuationToken(inner.Result)) :
                TryCatch<SingleRoundtripOptimisticExecutionContinuationToken>.FromException(inner.Exception);
        }
    }
}
