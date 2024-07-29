//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate.Aggregators
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class MakeListAggregator : IAggregator
    {
        private readonly List<CosmosElement> globalList;

        private MakeListAggregator(CosmosArray initialList)
        {
            this.globalList = new List<CosmosElement>();

            this.Aggregate(initialList);
        }

        public void Aggregate(CosmosElement localList)
        {
            if (!(localList is CosmosArray cosmosArray))
            {
                throw new ArgumentException($"{nameof(localList)} must be an array.");
            }

            this.globalList.AddRange(cosmosArray.ToList<CosmosElement>());
        }

        public CosmosElement GetResult()
        {
            return CosmosArray.Create(this.globalList);
        }

        public static TryCatch<IAggregator> TryCreate(CosmosElement continuationToken)
        {
            CosmosArray partialList;
            if (continuationToken != null)
            {
                if (!(continuationToken is CosmosArray cosmosPartialList))
                {
                    return TryCatch<IAggregator>.FromException(
                        new MalformedContinuationTokenException($@"Invalid MakeList continuation token: ""{continuationToken}""."));
                }

                partialList = cosmosPartialList;
            }
            else
            {
                partialList = CosmosArray.Empty;
            }

            return TryCatch<IAggregator>.FromResult(new MakeListAggregator(initialList: partialList));
        }
    }
}
