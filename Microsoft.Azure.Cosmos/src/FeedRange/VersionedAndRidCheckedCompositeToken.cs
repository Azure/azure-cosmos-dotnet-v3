// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal readonly struct VersionedAndRidCheckedCompositeToken
    {
        private static class PropertyNames
        {
            public const string Version = "V";
            public const string Rid = "Rid";
            public const string Continuation = "Continuation";
        }

        public VersionedAndRidCheckedCompositeToken(Version version, CosmosElement continuationToken, string rid)
        {
            this.VersionNumber = version;
            this.ContinuationToken = continuationToken ?? throw new ArgumentNullException(nameof(continuationToken));
            this.Rid = rid ?? throw new ArgumentNullException(nameof(rid));
        }

        public Version VersionNumber { get; }

        public CosmosElement ContinuationToken { get; }

        public string Rid { get; }

        public enum Version
        {
            V1 = 0,
            V2 = 2,
        }

        public static TryCatch<VersionedAndRidCheckedCompositeToken> MonadicCreateFromCosmosElement(CosmosElement cosmosElement)
        {
            if (cosmosElement == null)
            {
                throw new ArgumentNullException(nameof(cosmosElement));
            }

            if (!(cosmosElement is CosmosObject cosmosObject))
            {
                return TryCatch<VersionedAndRidCheckedCompositeToken>.FromException(
                    new FormatException($"Expected object for {nameof(VersionedAndRidCheckedCompositeToken)}: {cosmosElement}."));
            }

            if (!cosmosObject.TryGetValue(PropertyNames.Version, out CosmosNumber typeValue))
            {
                return TryCatch<VersionedAndRidCheckedCompositeToken>.FromException(
                    new FormatException($"expected number {nameof(PropertyNames.Version)} property for {nameof(VersionedAndRidCheckedCompositeToken)}: {cosmosElement}."));
            }

            if (!cosmosObject.TryGetValue(PropertyNames.Continuation, out CosmosElement continuationValue))
            {
                return TryCatch<VersionedAndRidCheckedCompositeToken>.FromException(
                    new FormatException($"expected object {nameof(PropertyNames.Continuation)} property for {nameof(VersionedAndRidCheckedCompositeToken)}: {cosmosElement}."));
            }

            if (!cosmosObject.TryGetValue(PropertyNames.Rid, out CosmosString ridValue))
            {
                return TryCatch<VersionedAndRidCheckedCompositeToken>.FromException(
                    new FormatException($"expected string {nameof(PropertyNames.Rid)} property for {nameof(VersionedAndRidCheckedCompositeToken)}: {cosmosElement}."));
            }

            VersionedAndRidCheckedCompositeToken token = new VersionedAndRidCheckedCompositeToken(
                (Version)Number64.ToLong(typeValue.Value),
                continuationValue,
                ridValue.Value);

            return TryCatch<VersionedAndRidCheckedCompositeToken>.FromResult(token);
        }

        public static CosmosElement ToCosmosElement(VersionedAndRidCheckedCompositeToken token)
        {
            return CosmosObject.Create(
                new Dictionary<string, CosmosElement>()
                {
                    { PropertyNames.Version, CosmosNumber64.Create((long)token.VersionNumber) },
                    { PropertyNames.Rid, CosmosString.Create(token.Rid) },
                    { PropertyNames.Continuation, token.ContinuationToken },
                });
        }
    }
}
