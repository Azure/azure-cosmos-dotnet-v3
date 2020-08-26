namespace Microsoft.Azure.Cosmos.Tests.Query.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public abstract class QueryTestBase
    {
        [FlagsAttribute]
        public enum CollectionTypes
        {
            None = 0,
            NonPartitioned = 0x1,
            SinglePartition = 0x2,
            MultiPartition = 0x4,
        }

        internal delegate Task Query(
            IDocumentContainer container,
            IReadOnlyList<CosmosObject> documents);

        internal delegate Task Query<T>(
            IDocumentContainer container,
            IReadOnlyList<CosmosObject> documents,
            T testArgs);

        internal async Task CreateIngestQueryDeleteAsync<T>(
            IContainerFactory containerFactory,
            CollectionTypes collectionTypes,
            IReadOnlyList<CosmosObject> documents,
            Query<T> query,
            T testArgs,
            string partitionKeyPath = "/id")
        {
            List<Task<(IDocumentContainer, IReadOnlyList<CosmosObject>)>> createAndIngestContainerTasks = new List<Task<(IDocumentContainer, IReadOnlyList<CosmosObject>)>>();
            foreach (CollectionTypes collectionType in Enum.GetValues(collectionTypes.GetType()).Cast<Enum>().Where(collectionTypes.HasFlag))
            {
                if (collectionType == CollectionTypes.None)
                {
                    continue;
                }

                IDocumentContainer container = collectionType switch
                {
                    CollectionTypes.NonPartitioned => await containerFactory.CreateNonPartitionedContainerAsync(),
                    CollectionTypes.SinglePartition => await containerFactory.CreateSinglePartitionContainerAsync(),
                    CollectionTypes.MultiPartition => await containerFactory.CreateMultiPartitionContainerAsync(),
                    _ => throw new ArgumentException($"Unknown {nameof(CollectionTypes)} : {collectionType}"),
                };

                createAndIngestContainerTasks.Add(IngestDocumentsAsync(container, documents));
            }

            await Task.WhenAll(createAndIngestContainerTasks);
            IReadOnlyList<(IDocumentContainer, IReadOnlyList<CosmosObject>)> containersAndDocuments = createAndIngestContainerTasks
                .Select(task => task.Result)
                .ToList();

            List<Task> queryTasks = new List<Task>();
            foreach ((IDocumentContainer container, IReadOnlyList<CosmosObject> insertedDocuments) in containersAndDocuments)
            {
                Task queryTask = query(container, insertedDocuments, testArgs);
                queryTasks.Add(queryTask);
            }

            await Task.WhenAll(queryTasks);

            // Todo delete the container.
        }

        internal static async Task<(IDocumentContainer, IReadOnlyList<CosmosObject>)> IngestDocumentsAsync(
            IDocumentContainer container,
            IReadOnlyList<CosmosObject> documents)
        {
            List<CosmosObject> insertedDocs = new List<CosmosObject>();
            foreach (CosmosObject document in documents)
            {
                Record record = await container.CreateItemAsync(document, cancellationToken: default);
                Dictionary<string, CosmosElement> keyValuePairs = new Dictionary<string, CosmosElement>
                {
                    ["_rid"] = CosmosString.Create(record.ResourceIdentifier.ToString()),
                    ["_ts"] = CosmosNumber64.Create(record.Timestamp),
                    ["id"] = CosmosString.Create(record.Identifier)
                };

                foreach (KeyValuePair<string, CosmosElement> property in record.Payload)
                {
                    keyValuePairs[property.Key] = property.Value;
                }

                insertedDocs.Add(CosmosObject.Create(keyValuePairs));
            }

            return (container, insertedDocs);
        }
    }
}
