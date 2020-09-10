//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.NetworkInformation;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Services.Management.Tests.LinqProviderTests;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class ClientTests
    {
        [TestMethod]
        public async Task ResourceResponseStreamingTest()
        {
            using (DocumentClient client = TestCommon.CreateClient(true))
            {

                Database db = (await client.CreateDatabaseAsync(new Database() { Id = Guid.NewGuid().ToString() })).Resource;
                DocumentCollection coll = await TestCommon.CreateCollectionAsync(client, db, new DocumentCollection()
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKey = new PartitionKeyDefinition()
                    {
                        Paths = new System.Collections.ObjectModel.Collection<string>() { "/id" }
                    }
                });
                ResourceResponse<Document> doc = await client.CreateDocumentAsync(coll.SelfLink, new Document() { Id = Guid.NewGuid().ToString() });

                Assert.AreEqual(doc.ResponseStream.Position, 0);

                StreamReader streamReader = new StreamReader(doc.ResponseStream);
                string text = streamReader.ReadToEnd();

                Assert.AreEqual(doc.ResponseStream.Position, doc.ResponseStream.Length);

                try
                {
                    doc.Resource.ToString();
                    Assert.Fail("Deserializing Resource here should throw exception since the stream was already read");
                }
                catch (JsonReaderException ex)
                {
                    Console.WriteLine("Expected exception while deserializing Resource: " + ex.Message);
                }
            }
        }

        [TestMethod]
        public async Task TestEtagOnUpsertOperationForHttpsClient()
        {
            await this.TestEtagOnUpsertOperation(false, Protocol.Https);
        }

        [TestMethod]
        public async Task TestEtagOnUpsertOperationForGatewayClient()
        {
            await this.TestEtagOnUpsertOperation(true);
        }

        [TestMethod]
        public async Task TestEtagOnUpsertOperationForDirectTCPClient()
        {
            await this.TestEtagOnUpsertOperation(false, Protocol.Tcp);
        }

        internal async Task TestEtagOnUpsertOperation(bool useGateway, Protocol protocol = Protocol.Tcp)
        {
            using (DocumentClient client = TestCommon.CreateClient(false, Protocol.Tcp))
            {
                Database db = (await client.CreateDatabaseAsync(new Database() { Id = Guid.NewGuid().ToString() })).Resource;
                DocumentCollection coll = await TestCommon.CreateCollectionAsync(client, db, new DocumentCollection()
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKey = new PartitionKeyDefinition()
                    {
                        Paths = new System.Collections.ObjectModel.Collection<string>() { "/id" }
                    }
                });

                LinqGeneralBaselineTests.Book myBook = new LinqGeneralBaselineTests.Book();
                myBook.Id = Guid.NewGuid().ToString();
                myBook.Title = "Azure DocumentDB 101";

                Document doc = (await client.CreateDocumentAsync(coll.SelfLink, myBook)).Resource;

                myBook.Title = "Azure DocumentDB 201";
                await client.ReplaceDocumentAsync(doc.SelfLink, myBook);

                AccessCondition condition = new AccessCondition();
                condition.Type = AccessConditionType.IfMatch;
                condition.Condition = doc.ETag;

                RequestOptions requestOptions = new RequestOptions();
                requestOptions.AccessCondition = condition;

                myBook.Title = "Azure DocumentDB 301";

                try
                {
                    await client.UpsertDocumentAsync(coll.SelfLink, myBook, requestOptions);
                    Assert.Fail("Upsert Document should fail since the Etag is not matching.");
                }
                catch (Exception ex)
                {
                    DocumentClientException innerException = ex as DocumentClientException;
                    Assert.AreEqual(HttpStatusCode.PreconditionFailed, innerException.StatusCode, "Invalid status code");
                }
            }
        }

        [TestMethod]
        public void SqlQuerySpecSerializationTest()
        {
            Action<string, SqlQuerySpec> verifyJsonSerialization = (expectedText, query) =>
            {
                string actualText = JsonConvert.SerializeObject(query);

                Assert.AreEqual(expectedText, actualText);

                SqlQuerySpec otherQuery = JsonConvert.DeserializeObject<SqlQuerySpec>(actualText);
                string otherText = JsonConvert.SerializeObject(otherQuery);
                Assert.AreEqual(expectedText, otherText);
            };

            Action<string> verifyJsonSerializationText = (text) =>
            {
                SqlQuerySpec query = JsonConvert.DeserializeObject<SqlQuerySpec>(text);
                string otherText = JsonConvert.SerializeObject(query);

                Assert.AreEqual(text, otherText);
            };

            // Verify serialization 
            verifyJsonSerialization("{\"query\":null}", new SqlQuerySpec());
            verifyJsonSerialization("{\"query\":\"SELECT 1\"}", new SqlQuerySpec("SELECT 1"));
            verifyJsonSerialization("{\"query\":\"SELECT 1\",\"parameters\":[{\"name\":null,\"value\":null}]}",
                new SqlQuerySpec()
                {
                    QueryText = "SELECT 1",
                    Parameters = new SqlParameterCollection() { new SqlParameter() }
                });

            verifyJsonSerialization("{\"query\":\"SELECT 1\",\"parameters\":[" +
                    "{\"name\":\"@p1\",\"value\":5}" +
                "]}",
                new SqlQuerySpec()
                {
                    QueryText = "SELECT 1",
                    Parameters = new SqlParameterCollection() { new SqlParameter("@p1", 5) }
                });
            verifyJsonSerialization("{\"query\":\"SELECT 1\",\"parameters\":[" +
                    "{\"name\":\"@p1\",\"value\":5}," +
                    "{\"name\":\"@p1\",\"value\":true}" +
                "]}",
                new SqlQuerySpec()
                {
                    QueryText = "SELECT 1",
                    Parameters = new SqlParameterCollection() { new SqlParameter("@p1", 5), new SqlParameter("@p1", true) }
                });
            verifyJsonSerialization("{\"query\":\"SELECT 1\",\"parameters\":[" +
                    "{\"name\":\"@p1\",\"value\":\"abc\"}" +
                "]}",
                new SqlQuerySpec()
                {
                    QueryText = "SELECT 1",
                    Parameters = new SqlParameterCollection() { new SqlParameter("@p1", "abc") }
                });
            verifyJsonSerialization("{\"query\":\"SELECT 1\",\"parameters\":[" +
                    "{\"name\":\"@p1\",\"value\":[1,2,3]}" +
                "]}",
                new SqlQuerySpec()
                {
                    QueryText = "SELECT 1",
                    Parameters = new SqlParameterCollection() { new SqlParameter("@p1", new int[] { 1, 2, 3 }) }
                });
            verifyJsonSerialization("{\"query\":\"SELECT 1\",\"parameters\":[" +
                    "{\"name\":\"@p1\",\"value\":{\"a\":[1,2,3]}}" +
                "]}",
                new SqlQuerySpec()
                {
                    QueryText = "SELECT 1",
                    Parameters = new SqlParameterCollection() { new SqlParameter("@p1", JObject.Parse("{\"a\":[1,2,3]}")) }
                });
            verifyJsonSerialization("{\"query\":\"SELECT 1\",\"parameters\":[" +
                    "{\"name\":\"@p1\",\"value\":{\"a\":[1,2,3]}}" +
                "]}",
                new SqlQuerySpec()
                {
                    QueryText = "SELECT 1",
                    Parameters = new SqlParameterCollection() { new SqlParameter("@p1", new JRaw("{\"a\":[1,2,3]}")) }
                });

            // Verify roundtrips
            verifyJsonSerializationText("{\"query\":null}");
            verifyJsonSerializationText("{\"query\":\"SELECT 1\"}");
            verifyJsonSerializationText(
                "{" +
                    "\"query\":\"SELECT 1\"," +
                    "\"parameters\":[" +
                        "{\"name\":null,\"value\":null}" +
                    "]" +
                "}");
            verifyJsonSerializationText(
                "{" +
                    "\"query\":\"SELECT 1\"," +
                    "\"parameters\":[" +
                        "{\"name\":\"@p1\",\"value\":null}" +
                    "]" +
                "}");
            verifyJsonSerializationText(
                "{" +
                    "\"query\":\"SELECT 1\"," +
                    "\"parameters\":[" +
                        "{\"name\":\"@p1\",\"value\":true}" +
                    "]" +
                "}");
            verifyJsonSerializationText(
                "{" +
                    "\"query\":\"SELECT 1\"," +
                    "\"parameters\":[" +
                        "{\"name\":\"@p1\",\"value\":false}" +
                    "]" +
                "}");
            verifyJsonSerializationText(
                "{" +
                    "\"query\":\"SELECT 1\"," +
                    "\"parameters\":[" +
                        "{\"name\":\"@p1\",\"value\":123}" +
                    "]" +
                "}");
            verifyJsonSerializationText(
                "{" +
                    "\"query\":\"SELECT 1\"," +
                    "\"parameters\":[" +
                        "{\"name\":\"@p1\",\"value\":\"abc\"}" +
                    "]" +
                "}");
            verifyJsonSerializationText(
                "{" +
                    "\"query\":\"SELECT 1\"," +
                    "\"parameters\":[" +
                        "{\"name\":\"@p1\",\"value\":{\"a\":[1,2,\"abc\"]}}" +
                    "]" +
                "}");
        }

        [TestMethod]
        public async Task VerifyNegativeWebProxySettings()
        {
            //proxy will be bypassed if the endpoint is localhost or 127.0.0.1
            string endpoint = $"https://{Environment.MachineName}";

            IWebProxy proxy = new WebProxy
            {
                Address = new Uri("http://www.cosmostestproxyshouldfail.com"),
                BypassProxyOnLocal = false,
                BypassList = new string[] { },
            };

            CosmosClient cosmosClient = new CosmosClient(
                endpoint,
                ConfigurationManager.AppSettings["MasterKey"],
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ConnectionProtocol = Protocol.Https,
                    WebProxy = proxy
                }
            );

            await Assert.ThrowsExceptionAsync<HttpRequestException>(async () =>
            {
                DatabaseResponse databaseResponse = await cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());
            });

            proxy = new TestWebProxy { Credentials = new NetworkCredential("test", "test") };

            cosmosClient = new CosmosClient(
                endpoint,
                ConfigurationManager.AppSettings["MasterKey"],
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ConnectionProtocol = Protocol.Https,
                    WebProxy = proxy
                }
            );

            await Assert.ThrowsExceptionAsync<HttpRequestException>(async () =>
            {
                DatabaseResponse databaseResponse = await cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());
            });
        }

        [TestMethod]
        public async Task HttpClientFactorySmokeTest()
        {
            HttpClient client = new HttpClient();
            Mock<Func<HttpClient>> factory = new Mock<Func<HttpClient>>();
            factory.Setup(f => f()).Returns(client);
            CosmosClient cosmosClient = new CosmosClient(
                ConfigurationManager.AppSettings["GatewayEndpoint"],
                ConfigurationManager.AppSettings["MasterKey"],
                new CosmosClientOptions
                {
                    ApplicationName = "test",
                    ConnectionMode = ConnectionMode.Gateway,
                    ConnectionProtocol = Protocol.Https,
                    HttpClientFactory = factory.Object
                }
            );

            string someId = Guid.NewGuid().ToString();
            Cosmos.Database database = null;
            try
            {
                database = await cosmosClient.CreateDatabaseAsync(someId);
                Cosmos.Container container = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id");
                await container.CreateItemAsync<dynamic>(new { id = someId });
                await container.ReadItemAsync<dynamic>(someId, new Cosmos.PartitionKey(someId));
                await container.DeleteItemAsync<dynamic>(someId, new Cosmos.PartitionKey(someId));
                await container.DeleteContainerAsync();
                Mock.Get(factory.Object).Verify(f => f(), Times.Once);
            }
            finally
            {
                if (database != null)
                {
                    await database.DeleteAsync();
                }
            }
        }

        [TestMethod]
        public async Task HttpClientConnectionLimitTest()
        {
            int gatewayConnectionLimit = 1;

            IReadOnlyList<string> excludeConnections = GetActiveConnections();
            CosmosClient cosmosClient = new CosmosClient(
                ConfigurationManager.AppSettings["GatewayEndpoint"],
                ConfigurationManager.AppSettings["MasterKey"],
                new CosmosClientOptions
                {
                    ApplicationName = "test",
                    GatewayModeMaxConnectionLimit = gatewayConnectionLimit,
                    ConnectionMode = ConnectionMode.Gateway,
                    ConnectionProtocol = Protocol.Https
                }
            );

            FieldInfo httpClient = cosmosClient.DocumentClient.GetType().GetField("httpClient", BindingFlags.NonPublic | BindingFlags.Instance);
            CosmosHttpClient cosmosHttpClient = (CosmosHttpClient)httpClient.GetValue(cosmosClient.DocumentClient);
            HttpClientHandler httpClientHandler = (HttpClientHandler)cosmosHttpClient.HttpMessageHandler;
            Assert.AreEqual(gatewayConnectionLimit, httpClientHandler.MaxConnectionsPerServer);

            Cosmos.Database database = await cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());
            Container container = await database.CreateContainerAsync(
                "TestConnections",
                "/pk",
                throughput: 20000);

            List<Task> creates = new List<Task>();
            for (int i = 0; i < 100; i++)
            {
                creates.Add(container.CreateItemAsync<dynamic>(new { id = Guid.NewGuid().ToString(), pk = Guid.NewGuid().ToString() }));
            }

            await Task.WhenAll(creates);

            // Verify the handler still exists after client warm up
            //Assert.AreEqual(gatewayConnectionLimit, httpClientHandler.MaxConnectionsPerServer);
            IReadOnlyList<string> afterConnections = GetActiveConnections();

            // Clean up the database and container
            await database.DeleteAsync();

            int connectionDiff = afterConnections.Count - excludeConnections.Count;
            Assert.IsTrue(connectionDiff <= gatewayConnectionLimit, $"Connection before : {excludeConnections.Count}, after {afterConnections.Count}");
        }

        public static IReadOnlyList<string> GetActiveConnections()
        {
            string testPid = Process.GetCurrentProcess().Id.ToString();
            using (Process p = new Process())
            {
                ProcessStartInfo ps = new ProcessStartInfo();
                ps.Arguments = "-a -n -o";
                ps.FileName = "netstat.exe";
                ps.UseShellExecute = false;
                ps.WindowStyle = ProcessWindowStyle.Hidden;
                ps.RedirectStandardInput = true;
                ps.RedirectStandardOutput = true;
                ps.RedirectStandardError = true;

                p.StartInfo = ps;
                p.Start();

                StreamReader stdOutput = p.StandardOutput;
                StreamReader stdError = p.StandardError;

                string content = stdOutput.ReadToEnd() + stdError.ReadToEnd();
                string exitStatus = p.ExitCode.ToString();

                if (exitStatus != "0")
                {
                    // Command Errored. Handle Here If Need Be
                }

                List<string> connections = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => x.EndsWith(testPid)).ToList();

                Assert.IsTrue(connections.Count > 0);

                return connections;
            }
        }
    }

    internal static class StringHelper
    {
        internal static string EscapeForSQL(this string input)
        {
            return input.Replace("'", "\\'").Replace("\"", "\\\"");
        }
    }

    internal class TestWebProxy : IWebProxy
    {
        public ICredentials Credentials { get; set; }

        public Uri GetProxy(Uri destination)
        {
            return new Uri("http://www.cosmostestproxyshouldfail.com");
        }

        public bool IsBypassed(Uri host)
        {
            return false;
        }
    }
}
