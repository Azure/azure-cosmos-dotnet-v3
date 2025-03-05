//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class InMemoryCollectionTests : DocumentContainerTests
    {
        internal override IDocumentContainer CreateDocumentContainer(
            PartitionKeyDefinition partitionKeyDefinition,
            int numItems = 0,
            FlakyDocumentContainer.FailureConfigs failureConfigs = null)
        {
            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(partitionKeyDefinition);
            if (failureConfigs != null)
            {
                monadicDocumentContainer = new FlakyDocumentContainer(monadicDocumentContainer, failureConfigs);
            }

            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);

            for (int i = 0; i < numItems; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                documentContainer.CreateItemAsync(item, cancellationToken: default).Wait();
            }

            return documentContainer;
        }
    }
}