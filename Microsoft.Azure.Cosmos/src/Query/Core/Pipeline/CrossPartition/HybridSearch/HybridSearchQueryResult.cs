// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal readonly struct HybridSearchQueryResult
    {
        public CosmosString Rid { get; }

        public CosmosArray ComponentScores { get; }

        public CosmosElement Payload { get; }

        public double Score { get; }

        private HybridSearchQueryResult(CosmosString rid, CosmosArray componentScores, CosmosElement payload, double score)
        {
            this.Rid = rid ?? throw new ArgumentNullException(nameof(rid));
            this.ComponentScores = componentScores ?? throw new ArgumentNullException(nameof(componentScores));
            this.Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            this.Score = score;
        }

        private HybridSearchQueryResult(CosmosString rid, CosmosArray componentScores, CosmosElement payload)
            : this(rid, componentScores, payload, 0)
        {
        }

        public HybridSearchQueryResult WithScore(double score)
        {
            return new HybridSearchQueryResult(this.Rid, this.ComponentScores, this.Payload, score);
        }

        public static HybridSearchQueryResult Create(CosmosElement document)
        {
            CosmosObject cosmosObject = document as CosmosObject;

            if (cosmosObject == null)
            {
                throw new ArgumentException($"{nameof(document)} must be an object.");
            }

            if (!cosmosObject.TryGetValue(FieldNames.Rid, out CosmosString rid))
            {
                throw new ArgumentException($"{FieldNames.Rid} must exist.");
            }

            if (!cosmosObject.TryGetValue(FieldNames.Payload, out CosmosObject outerPayload))
            {
                throw new ArgumentException($"{FieldNames.Payload} must exist.");
            }

            if (!outerPayload.TryGetValue(FieldNames.Payload, out CosmosObject innerPayload))
            {
                throw new ArgumentException($"{FieldNames.Payload} must exist nested within the outer payload field.");
            }

            if (!outerPayload.TryGetValue(FieldNames.ComponentScores, out CosmosArray componentScores))
            {
                throw new ArgumentException($"{FieldNames.ComponentScores} must exist.");
            }

            return new HybridSearchQueryResult(rid, componentScores, innerPayload);
        }

        private static class FieldNames
        {
            public const string Rid = "_rid";

            public const string Payload = "payload";

            public const string ComponentScores = "componentScores";
        }
    }
}
