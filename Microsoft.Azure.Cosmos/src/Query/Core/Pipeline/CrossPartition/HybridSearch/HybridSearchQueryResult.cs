// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using ResourceId = Microsoft.Azure.Documents.ResourceId;

    internal readonly struct HybridSearchQueryResult
    {
        public CosmosString Rid { get; }

        public ulong DocumentRid { get; }

        public CosmosArray ComponentScores { get; }

        public CosmosElement Payload { get; }

        public double Score { get; }

        private HybridSearchQueryResult(CosmosString rid, ulong documentRid, CosmosArray componentScores, CosmosElement payload, double score)
        {
            this.Rid = rid ?? throw new ArgumentNullException(nameof(rid));
            this.DocumentRid = documentRid;
            this.ComponentScores = componentScores ?? throw new ArgumentNullException(nameof(componentScores));
            this.Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            this.Score = score;
        }

        public HybridSearchQueryResult WithScore(double score)
        {
            return new HybridSearchQueryResult(this.Rid, this.DocumentRid, this.ComponentScores, this.Payload, score);
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

            ulong documentRid = ResourceId.Parse(rid.Value).Document;
            bool outerPayloadExists = cosmosObject.TryGetValue(FieldNames.Payload, out CosmosObject outerPayload);

            HybridSearchQueryResult result;
            if (outerPayloadExists && outerPayload.TryGetValue(FieldNames.ComponentScores, out CosmosArray componentScores))
            {
                // Using the older format where the payload is nested.
                if (!outerPayload.TryGetValue(FieldNames.Payload, out CosmosElement innerPayload))
                {
                    innerPayload = CosmosUndefined.Create();
                }

                result = new HybridSearchQueryResult(rid, documentRid, componentScores, innerPayload, 0);
            }
            else
            {
                // Using the newer format where the payload is not nested.
                if (!cosmosObject.TryGetValue(FieldNames.ComponentScores, out componentScores))
                {
                    throw new ArgumentException($"{FieldNames.ComponentScores} must exist.");
                }

                if (!cosmosObject.TryGetValue(FieldNames.Payload, out CosmosElement payload))
                {
                    payload = CosmosUndefined.Create();
                }

                result = new HybridSearchQueryResult(rid, documentRid, componentScores, payload, 0);
            }

            return result;
        }

        private static class FieldNames
        {
            public const string Rid = "_rid";

            public const string Payload = "payload";

            public const string ComponentScores = "componentScores";
        }
    }
}
