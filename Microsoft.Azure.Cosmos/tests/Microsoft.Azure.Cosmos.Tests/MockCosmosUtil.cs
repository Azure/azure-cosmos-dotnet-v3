//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Client.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Security;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Moq;
    using Newtonsoft.Json;

    internal class MockCosmosUtil
    {
        public static CosmosClient CreateMockCosmosClient(Action<CosmosClientBuilder> customizeClientBuilder = null)
        {
            DocumentClient documentClient = new MockDocumentClient();
            
            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder("http://localhost", Guid.NewGuid().ToString());
            if (customizeClientBuilder != null)
            {
                customizeClientBuilder(cosmosClientBuilder);
            }

            return cosmosClientBuilder.Build(documentClient);
        }

        public static Mock<CosmosContainerCore> CreateMockContainer(
            string dbName = "myDb",
            string containerName = "myContainer")
        {
            Uri link = new Uri($"/dbs/{dbName}/colls/{containerName}" , UriKind.Relative);
            Mock<CosmosContainerCore> mockContainer = new Mock<CosmosContainerCore>();
            mockContainer.Setup(x => x.LinkUri).Returns(link);
            return mockContainer;
        }

        public static CosmosClientConfiguration GetDefaultConfiguration()
        {
            return new CosmosClientConfiguration(accountEndPoint: "http://localhost", accountKey: "MockedCosmosClientAccountKeyDummyValue");
        }
    }
}
