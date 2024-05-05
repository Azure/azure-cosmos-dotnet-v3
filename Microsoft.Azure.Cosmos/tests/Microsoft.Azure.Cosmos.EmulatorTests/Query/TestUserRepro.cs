namespace Microsoft.Azure.Cosmos.Query
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    [TestClass]
    public class TestUserRepro
    {
        [TestMethod]
        public async Task ODEIncorrectResults()
        {
            string databaseName = "DataVerseRepro";
            string containerName = "c1";

            CosmosClient cosmosClient = new CosmosClient(@"AccountEndpoint=https://localhost:8081/;");

            DatabaseResponse dbResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
            Container container;
            if (dbResponse.StatusCode == System.Net.HttpStatusCode.Created)
            {
                container = await this.CreateContainer(cosmosClient, databaseName, containerName);
            }
            else
            {
                container = cosmosClient.GetContainer(databaseName, containerName);
                await container.DeleteContainerAsync();
                container = await this.CreateContainer(cosmosClient, databaseName, containerName);
            }

            const string queryText = @"
                SELECT c.props.title 
                FROM c 
                WHERE ((
                            (c.isdel = false) 
                            AND (c.otc = 2517)
                        ) 
                        AND (c.orgId = ""1936a1b9-2367-4be9-8207-64c57185be62"")
                    )
                GROUP BY c.props.title 
                OFFSET 1 LIMIT 1";

            List<string> odeResults = new();
            {
                FeedIterator<object> iterator = container.GetItemQueryIterator<object>(
                    queryText,
                    requestOptions: new QueryRequestOptions()
                    {
                        EnableOptimisticDirectExecution = true,
                        PartitionKey = new PartitionKey(@"1936a1b9-2367-4be9-8207-64c57185be62|p1")
                    });
                while(iterator.HasMoreResults)
                {
                    FeedResponse<object> feedResponse = await iterator.ReadNextAsync();
                    odeResults.AddRange(feedResponse.Resource.Select(obj => obj.ToString()));
                }
            }

            List<string> nonOdeResults = new();
            {
                FeedIterator<object> iterator = container.GetItemQueryIterator<object>(
                    queryText,
                    requestOptions: new QueryRequestOptions()
                    {
                        EnableOptimisticDirectExecution = false,
                        PartitionKey = new PartitionKey(@"1936a1b9-2367-4be9-8207-64c57185be62|p1")
                    });
                while (iterator.HasMoreResults)
                {
                    FeedResponse<object> feedResponse = await iterator.ReadNextAsync();
                    nonOdeResults.AddRange(feedResponse.Resource.Select(obj => obj.ToString()));
                }
            }

            Assert.AreEqual(odeResults.Count, nonOdeResults.Count);
            for (int i = 0; i < odeResults.Count; i++)
            {
                Assert.AreEqual(odeResults[i], nonOdeResults[i]);
            }
        }

        private async Task<Container> CreateContainer(CosmosClient cosmosClient, string databaseName, string containerName)
        {
            Database db = cosmosClient.GetDatabase(databaseName);
            ContainerResponse containerResponse = await db.CreateContainerAsync(new ContainerProperties(
                containerName,
                new Documents.PartitionKeyDefinition()
                {
                    Paths = new System.Collections.ObjectModel.Collection<string> { "/partitionKey" }
                })
                );

            Container container = cosmosClient.GetContainer(databaseName, containerName);

            string[] logicalPartitions = new[]
            {
                "1936a1b9-2367-4be9-8207-64c57185be62|p1",
                "1936a1b9-2367-4be9-8207-64c57185be62|p2"
            };
            //string documentFormat = @"
            //        {{
            //            ""otc"": 2517,
            //            ""parentId"": ""ParentId824141248"",
            //            ""props"": {{
            //                ""attribute224733476"": ""value1273104614"",
            //                ""attribute174153363"": 1341963442,
            //                ""title"": ""{0}""
            //            }},
            //            ""orgId"": ""1936a1b9-2367-4be9-8207-64c57185be62"",
            //            ""id"": ""{1}"",
            //            ""size"": 546,
            //            ""createdon"": 1714780451,
            //            ""tst"": 638503772519228400,
            //            ""isdel"": false,
            //            ""partitionKey"": ""{2}"",
            //            ""_etag"": ""\""00000000-0000-0000-9db5-324b220401da\"""",
            //            ""ttl"": -1,
            //            ""_rid"": ""sDJwAN9nF3YCAAAAAAAAAA=="",
            //            ""_self"": ""dbs/sDJwAA==/colls/sDJwAN9nF3Y=/docs/sDJwAN9nF3YCAAAAAAAAAA==/"",
            //            ""_attachments"": ""attachments/"",
            //            ""_ts"": 1714780451
            //        }}";
            string documentFormat = @"
                    {{
                        ""otc"": 2517,
                        ""props"": {{
                            ""title"": ""{0}""
                        }},
                        ""orgId"": ""1936a1b9-2367-4be9-8207-64c57185be62"",
                        ""id"": ""{1}"",
                        ""isdel"": false,
                        ""partitionKey"": ""{2}""
                    }}";
            foreach (string logicalPartition in logicalPartitions)
            {
                for (int i = 0; i < 3; i++)
                {
                    string document = string.Format(documentFormat, i, i, logicalPartition);
                    PartitionKey partitionKey = new PartitionKey(logicalPartition);
                    ResponseMessage response = await container.CreateItemStreamAsync(
                        this.ToStream(document),
                        partitionKey);
                    Assert.AreEqual(System.Net.HttpStatusCode.Created, response.StatusCode);
                }
            }

            return container;
        }

        private Stream ToStream(string stringValue)
        {
            MemoryStream stream = new();
            StreamWriter writer = new(stream);
            writer.Write(stringValue);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
    }
}
