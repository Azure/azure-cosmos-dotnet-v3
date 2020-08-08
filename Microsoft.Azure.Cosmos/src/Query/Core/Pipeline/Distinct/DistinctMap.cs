//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    /// <summary>
    /// Base class for all types of DistinctMaps.
    /// An IDistinctMap is able to efficiently store a hash set of json values.
    /// This is done by taking the json value and storing a GUID like hash of that value in a hashset.
    /// By storing the hash we avoid storing the entire object in main memory.
    /// Only downside is that there is a possibility of a hash collision.
    /// However we store the hash as 192 bits, so the possibility of a collision is pretty low.
    /// You can run the birthday paradox math to figure out how low: https://en.wikipedia.org/wiki/Birthday_problem
    /// </summary>
    internal abstract partial class DistinctMap
    {
        /// <summary>
        /// Creates an IDistinctMap based on the type.
        /// </summary>
        /// <param name="distinctQueryType">The type of distinct query.</param>
        /// <param name="distinctMapContinuationToken">The continuation token to resume from.</param>
        /// <returns>The appropriate IDistinctMap.</returns>
        public static TryCatch<DistinctMap> TryCreate(
            DistinctQueryType distinctQueryType,
            CosmosElement distinctMapContinuationToken) => distinctQueryType switch
            {
                DistinctQueryType.None => throw new ArgumentException("distinctQueryType can not be None. This part of code is not supposed to be reachable. Please contact support to resolve this issue."),
                DistinctQueryType.Unordered => UnorderdDistinctMap.TryCreate(distinctMapContinuationToken),
                DistinctQueryType.Ordered => OrderedDistinctMap.TryCreate(distinctMapContinuationToken),
                _ => throw new ArgumentException($"Unrecognized DistinctQueryType: {distinctQueryType}."),
            };

        /// <summary>
        /// Adds a JToken to this DistinctMap.
        /// </summary>
        /// <param name="cosmosElement">The element to add.</param>
        /// <param name="hash">The hash of the cosmos element</param>
        /// <returns>Whether or not the token was successfully added.</returns>
        public abstract bool Add(CosmosElement cosmosElement, out UInt128 hash);

        public abstract string GetContinuationToken();

        public abstract CosmosElement GetCosmosElementContinuationToken();
    }
}
