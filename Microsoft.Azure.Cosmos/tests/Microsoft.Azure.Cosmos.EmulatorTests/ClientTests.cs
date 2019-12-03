//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Services.Management.Tests.LinqProviderTests;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
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

            await Assert.ThrowsExceptionAsync<HttpRequestException>(async () => {
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

            await Assert.ThrowsExceptionAsync<HttpRequestException>(async () => {
                DatabaseResponse databaseResponse = await cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());
            });
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
