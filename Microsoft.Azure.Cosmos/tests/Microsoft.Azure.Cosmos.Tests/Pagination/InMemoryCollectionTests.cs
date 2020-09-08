//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class InMemoryCollectionTests : DocumentContainerTests
    {
        internal override IDocumentContainer CreateDocumentContainer(
            PartitionKeyDefinition partitionKeyDefinition,
            FlakyDocumentContainer.FailureConfigs failureConfigs = null)
        {
            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(partitionKeyDefinition);
            if(failureConfigs != null)
            {
                monadicDocumentContainer = new FlakyDocumentContainer(monadicDocumentContainer, failureConfigs);
            }

            return new DocumentContainer(monadicDocumentContainer);
        }
    }
}
