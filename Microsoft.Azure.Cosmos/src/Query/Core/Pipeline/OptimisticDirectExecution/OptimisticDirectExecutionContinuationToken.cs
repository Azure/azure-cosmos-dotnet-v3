//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.OptimisticDirectExecutionQuery
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// A continuation token that has both backend continuation token and partition range information. 
    /// </summary>
    internal sealed class OptimisticDirectExecutionContinuationToken : IPartitionedToken
    {
        private const string OptimisticDirectExecutionToken = "OptimisticDirectExecutionToken";

        public OptimisticDirectExecutionContinuationToken(ParallelContinuationToken token)
        {
            this.Token = token;
        }

        public ParallelContinuationToken Token { get; }

        public Range<string> Range => this.Token.Range;

        public static CosmosElement ToCosmosElement(OptimisticDirectExecutionContinuationToken continuationToken)
        {
            CosmosElement inner = ParallelContinuationToken.ToCosmosElement(continuationToken.Token);
            return CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                [OptimisticDirectExecutionToken] = inner
                            });
        }

        public static TryCatch<OptimisticDirectExecutionContinuationToken> TryCreateFromCosmosElement(CosmosElement cosmosElement)
        {
            CosmosObject cosmosObjectContinuationToken = cosmosElement as CosmosObject;
            if (cosmosObjectContinuationToken == null)
            {
                return TryCatch<OptimisticDirectExecutionContinuationToken>.FromException(
                                    new MalformedChangeFeedContinuationTokenException(
                                        message: $"Malformed Continuation Token"));
            }

            TryCatch<ParallelContinuationToken> inner = ParallelContinuationToken.TryCreateFromCosmosElement(cosmosObjectContinuationToken[OptimisticDirectExecutionToken]);

            return inner.Succeeded ?
                TryCatch<OptimisticDirectExecutionContinuationToken>.FromResult(new OptimisticDirectExecutionContinuationToken(inner.Result)) :
                TryCatch<OptimisticDirectExecutionContinuationToken>.FromException(inner.Exception);
        }
    }
}
