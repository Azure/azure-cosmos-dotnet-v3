//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.TryExecuteQuery
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;

    /// <summary>
    /// A continuation token that has both backend continuation token and partition range information. 
    /// </summary>
    internal sealed class TryExecuteContinuationToken : IPartitionedToken
    {
        public TryExecuteContinuationToken(CosmosElement token, Range<string> range)
            : this(new ParallelContinuationToken((token as CosmosString)?.Value, range))
        {
        }

        public TryExecuteContinuationToken(ParallelContinuationToken token)
        {
            this.Token = token;
        }

        public ParallelContinuationToken Token { get; }

        public Range<string> Range => this.Token.Range;

        public static CosmosElement ToCosmosElement(TryExecuteContinuationToken continuationToken)
        {
            CosmosElement inner = ParallelContinuationToken.ToCosmosElement(continuationToken.Token);
            return CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                ["tryExecute"] = inner
                            });
        }

        public static TryCatch<TryExecuteContinuationToken> TryCreateFromCosmosElement(CosmosElement cosmosElement)
        {
            TryCatch<ParallelContinuationToken> inner = ParallelContinuationToken.TryCreateFromCosmosElement(cosmosElement);

            return inner.Succeeded ?
                TryCatch<TryExecuteContinuationToken>.FromResult(new TryExecuteContinuationToken(inner.Result)) :
                TryCatch<TryExecuteContinuationToken>.FromException(inner.Exception);
        }
    }
}
