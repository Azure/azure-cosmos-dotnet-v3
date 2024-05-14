//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate.Aggregators
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
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
            foreach (CosmosElement setItem in initialList)
            {
                this.globalList.Add(setItem);
            }
        }

        public void Aggregate(CosmosElement localList)
        {
            if (!(localList is CosmosArray cosmosArray))
            {
                throw new ArgumentException($"{nameof(localList)} must be an array.");
            }

            foreach (CosmosElement listItem in cosmosArray)
            {
                this.globalList.Add(listItem);
            }
        }

        public CosmosElement GetResult()
        {
            CosmosElement[] cosmosElementArray = new CosmosElement[this.globalList.Count];
            this.globalList.CopyTo(cosmosElementArray);
            return CosmosArray.Create(cosmosElementArray);
        }

        public string GetContinuationToken()
        {
            return this.globalList.ToString();
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

            return TryCatch<IAggregator>.FromResult(
                new MakeListAggregator(initialList: partialList));
        }

        public CosmosElement GetCosmosElementContinuationToken()
        {
            CosmosElement[] cosmosElementArray = new CosmosElement[this.globalList.Count];
            this.globalList.CopyTo(cosmosElementArray);
            return CosmosArray.Create(cosmosElementArray);
        }
    }
}
