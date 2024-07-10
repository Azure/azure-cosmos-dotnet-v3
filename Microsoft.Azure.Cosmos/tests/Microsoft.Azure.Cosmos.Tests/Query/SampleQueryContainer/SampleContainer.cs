namespace Microsoft.Azure.Cosmos.Tests.Query.SampleQueryContainer
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    internal class SampleContainer
    {
        private SampleContainer(IEnumerable<SampleDocument> documents, IEnumerable<Partition> partitions)
        {
            Debug.Assert(documents.Count() == partitions.Select(partition => partition.Documents.Count()).Sum(),
                "LogicalContainer Assert!",
                "All documents must belong to some partition!");

            this.Documents = documents.ToList();
            this.Partitions = partitions.ToList();
        }

        public IEnumerable<string> GetPartitionKeyDefinitionPaths()
        {
            yield return "/TenantId";
            yield return "/UserId";
            yield return "/SessionId";
        }

        public IReadOnlyList<Partition> Partitions { get; }

        public IReadOnlyList<SampleDocument> Documents { get; }

        public static SampleContainer CreateContainerSplitAtLevel1()
        {
            // ISSUE-TODO-adityasa-2024/3/24 - It will be better to create the partitioned data as part of GenerateContainerData.
            // At the very least it would save a bunch of consistency checks spread around.
            (IReadOnlyList<SampleDocument> documents, IReadOnlyList<LogicalPartitionKey> sortedPartitionKeys) = GenerateContainerData();
            IReadOnlyList<LogicalPartitionRange> partitionRanges = CreatePartitionRangesWithCommonPrefix(sortedPartitionKeys, commonPrefixComponentCount: 1);

            foreach (SampleDocument d in documents)
            {
                LogicalPartitionKey documentPartitionKey = d.LogicalPartitionKey;
                List<LogicalPartitionRange> containingRanges = partitionRanges.Where(range => range.Contains(documentPartitionKey)).ToList();
                Debug.Assert(
                    containingRanges.Count() == 1,
                    "LogicalContainer Assert!",
                    "Each document is expected to belong to exactly one partition range");
            }

            List<Partition> partitions = new List<Partition>();
            foreach (LogicalPartitionRange partitionRange in partitionRanges)
            {
                List<SampleDocument> partitionedDocuments = new List<SampleDocument>();
                foreach (SampleDocument document in documents)
                {
                    if (partitionRange.Contains(document.LogicalPartitionKey))
                    {
                        partitionedDocuments.Add(document);
                    }
                }

                partitions.Add(new Partition(partitionedDocuments, partitionRange));
            }

            return new SampleContainer(documents, partitions);
        }

        private static IReadOnlyList<LogicalPartitionRange> CreatePartitionRangesWithCommonPrefix(IReadOnlyList<LogicalPartitionKey> sortedPartitionKeys, int commonPrefixComponentCount)
        {
            if (commonPrefixComponentCount != 1)
            {
                throw new NotSupportedException($"Currently only splitting at top level is supported");
            }

            List<LogicalPartitionRange> ranges = new();
            List<LogicalPartitionKey> previousChunk = new();
            string previousPrefix = null;
            LogicalPartitionKey min = null, max = null;

            foreach (LogicalPartitionKey partitionKey in sortedPartitionKeys)
            {
                string prefix = GetPrefix(partitionKey, commonPrefixComponentCount);

                previousPrefix ??= prefix;

                if (previousPrefix == prefix)
                {
                    previousChunk.Add(partitionKey);
                }
                else
                {
                    if (commonPrefixComponentCount != 3)
                    {
                        Debug.Assert(previousChunk.Count > 2, "LogicalContainer Assert!", "At least 2 values with common prefix must be present with common prefix for accurate repdocution of the expected environment.");
                    }

                    // We split second partition into 3 chunks.
                    if (ranges.Count == 1)
                    {
                        max = previousChunk[previousChunk.Count / 3];
                        ranges.Add(new LogicalPartitionRange(min, max));
                        min = max;

                        max = previousChunk[(2 * previousChunk.Count) / 3];
                        ranges.Add(new LogicalPartitionRange(min, max));
                        min = max;
                    }
                    else
                    {
                        max = previousChunk[previousChunk.Count / 2];
                        ranges.Add(new LogicalPartitionRange(min, max));
                        min = max;
                    }
                    previousPrefix = prefix;
                    previousChunk = new() { partitionKey };
                }
            }

            // Split the last chunk
            max = previousChunk[previousChunk.Count / 2];
            ranges.Add(new LogicalPartitionRange(min, max));
            min = max;
            ranges.Add(new LogicalPartitionRange(min, null));

            Debug.Assert(ranges.Count > 0, "LogicalContainer Assert!", "At least 1 split point must be created.");

            return ranges;
        }

        private static string GetPrefix(LogicalPartitionKey partitionKey, int commonPrefixComponentCount)
        {
            Debug.Assert(commonPrefixComponentCount > 0 && commonPrefixComponentCount < 3, "LogicalContainer Assert!", $"commonPrefixComponentCount must be within [1, 3]. Received {commonPrefixComponentCount}");

            // Each hash is 16 bytes => 32 hex characters long. There's an additional hyphen after each byte except last.
            // This makes it 48 characters for each common segment.
            return partitionKey.PhysicalPartitionKey.Hash.Substring(0, commonPrefixComponentCount * 48 - 1);
        }

        public static (IReadOnlyList<SampleDocument>, IReadOnlyList<LogicalPartitionKey>) GenerateContainerData()
        {
            IReadOnlyList<SampleDocument> documents = GenerateData(tenantCount: 3, userCount: 3, sessionCount: 3, idCount: 3);

            // Important - we have to sort the partition keys by the physical hashes and not logical values.
            // Murmur hash (like most) does not preserve order of original objects.
            IReadOnlyList<LogicalPartitionKey> sortedPartitionKeys = documents
                .Select(document => document.LogicalPartitionKey)
                .OrderBy(logicalPartitionKey => logicalPartitionKey.PhysicalPartitionKey.Hash, StringComparer.Ordinal)
                .ToList();
            return (documents, sortedPartitionKeys);
        }

        internal static IReadOnlyList<SampleDocument> GenerateData(int tenantCount, int userCount, int sessionCount, int idCount)
        {
            List<SampleDocument> documents = new List<SampleDocument>();
            for (int tenantId = 0; tenantId < tenantCount; tenantId++)
            {
                for (int userId = 0; userId < userCount; userId++)
                {
                    for (int sessionId = 0; sessionId < sessionCount; sessionId++)
                    {
                        for (int id = 0; id < idCount; id++)
                        {
                            documents.Add(
                                new SampleDocument(
                                    FormatTenantValue(tenantId),
                                    FormatUserValue(userId),
                                    FormatSessionValue(sessionId),
                                    FormatIdValue(tenantId, userId, sessionId, id)));
                        }
                    }
                }
            }

            return documents;
        }

        internal static string FormatTenantValue(int tenantId) => $"Tenant_{tenantId}";

        internal static string FormatUserValue(int userId) => $"User_{userId}";

        internal static string FormatSessionValue(int sessionId) => $"Session_{sessionId}";

        internal static string FormatIdValue(int tenantId, int userId, int sessionId, int id) =>
            $"{FormatTenantValue(tenantId)}|{FormatUserValue(userId)}|{FormatSessionValue(sessionId)}|Id_{id}";
    }
}
