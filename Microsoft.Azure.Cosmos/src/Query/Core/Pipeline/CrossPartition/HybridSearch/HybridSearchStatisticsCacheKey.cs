//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch
{
    using System;
    using System.Collections.Generic;

    // Identifies one cached scalar statistic.
    internal sealed class HybridSearchStatisticsCacheKey : IEquatable<HybridSearchStatisticsCacheKey>
    {
        private readonly string containerResourceId;
        private readonly string containerETag;
        private readonly KeyKind kind;
        private readonly IReadOnlyList<string> path;
        private readonly string term;
        private readonly int? distance;
        private readonly int hashCode;

        private HybridSearchStatisticsCacheKey(
            string containerResourceId,
            string containerETag,
            KeyKind kind,
            IReadOnlyList<string> path,
            string term,
            int? distance)
        {
            HashCode hash = new HashCode();
            hash.Add(containerResourceId);
            hash.Add(containerETag);
            hash.Add(kind);

            // The path is not copied: query plans are not mutated after they are created.
            if (path != null)
            {
                for (int index = 0; index < path.Count; index++)
                {
                    hash.Add(path[index]);
                }
            }

            hash.Add(term);
            hash.Add(distance);

            this.containerResourceId = containerResourceId;
            this.containerETag = containerETag;
            this.kind = kind;
            this.path = path;
            this.term = term;
            this.distance = distance;
            this.hashCode = hash.ToHashCode();
        }

        private enum KeyKind
        {
            TotalDocumentCount,
            DocumentPathWordCount,
            DocumentPathTermHitCount,
        }

        public static HybridSearchStatisticsCacheKey CreateTotalDocumentCount(string containerResourceId, string containerETag)
        {
            return new HybridSearchStatisticsCacheKey(
                containerResourceId ?? throw new ArgumentNullException(nameof(containerResourceId)),
                containerETag ?? throw new ArgumentNullException(nameof(containerETag)),
                KeyKind.TotalDocumentCount,
                null,
                null,
                null);
        }

        public static HybridSearchStatisticsCacheKey CreateDocumentPathWordCount(
            string containerResourceId,
            string containerETag,
            IReadOnlyList<string> path)
        {
            return new HybridSearchStatisticsCacheKey(
                containerResourceId ?? throw new ArgumentNullException(nameof(containerResourceId)),
                containerETag ?? throw new ArgumentNullException(nameof(containerETag)),
                KeyKind.DocumentPathWordCount,
                path ?? throw new ArgumentNullException(nameof(path)),
                null,
                null);
        }

        public static HybridSearchStatisticsCacheKey CreateDocumentPathTermHitCount(
            string containerResourceId,
            string containerETag,
            IReadOnlyList<string> path,
            string term,
            int? distance)
        {
            return new HybridSearchStatisticsCacheKey(
                containerResourceId ?? throw new ArgumentNullException(nameof(containerResourceId)),
                containerETag ?? throw new ArgumentNullException(nameof(containerETag)),
                KeyKind.DocumentPathTermHitCount,
                path ?? throw new ArgumentNullException(nameof(path)),
                term ?? throw new ArgumentNullException(nameof(term)),
                distance);
        }

        public bool Equals(HybridSearchStatisticsCacheKey other)
        {
            if (other == null ||
                this.kind != other.kind ||
                this.distance != other.distance ||
                !string.Equals(this.containerResourceId, other.containerResourceId, StringComparison.Ordinal) ||
                !string.Equals(this.containerETag, other.containerETag, StringComparison.Ordinal) ||
                !string.Equals(this.term, other.term, StringComparison.Ordinal))
            {
                return false;
            }

            if (this.path == null || other.path == null)
            {
                return this.path == other.path;
            }

            if (this.path.Count != other.path.Count)
            {
                return false;
            }

            for (int index = 0; index < this.path.Count; index++)
            {
                if (!string.Equals(this.path[index], other.path[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) => obj is HybridSearchStatisticsCacheKey other && this.Equals(other);

        public override int GetHashCode() => this.hashCode;
    }
}
