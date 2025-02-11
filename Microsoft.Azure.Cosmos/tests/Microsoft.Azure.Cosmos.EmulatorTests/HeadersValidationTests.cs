//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class HeadersValidationTests
    {
        private static byte BinarySerializationByteMarkValue = 128;

        private string currentVersion;
        private byte[] currentVersionUTF8;

        [ClassInitialize]
        public static void Initialize(TestContext textContext)
        {
            // Create a CosmosClient to ensure the correct HttpConstants.Versions is being used
            TestCommon.CreateCosmosClient();
            DocumentClientSwitchLinkExtension.Reset("HeadersValidationTests");
        }

        [TestInitialize]
        public async Task Startup()
        {
            this.currentVersion = HttpConstants.Versions.CurrentVersion;
            this.currentVersionUTF8 = HttpConstants.Versions.CurrentVersionUTF8;

            //var client = TestCommon.CreateClient(false, Protocol.Tcp);
            using var client = TestCommon.CreateClient(true);
            await TestCommon.DeleteAllDatabasesAsync();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            HttpConstants.Versions.CurrentVersion = this.currentVersion;
            HttpConstants.Versions.CurrentVersionUTF8 = this.currentVersionUTF8;
        }

        [TestMethod]
        public void ValidatePageSizeRntbd()
        {
            using var client = TestCommon.CreateClient(false, Protocol.Tcp);
            ValidatePageSize(client);
        }

        [TestMethod]
        public void ValidatePageSizeGatway()
        {
            using var client = TestCommon.CreateClient(true);
            ValidatePageSize(client);
        }

        private void ValidatePageSize(DocumentClient client)
        {
            // Invalid parsing
            INameValueCollection headers = new Documents.Collections.RequestNameValueCollection
            {
                { HttpConstants.HttpHeaders.PageSize, "\"Invalid header type\"" }
            };

            try
            {
                ReadDatabaseFeedRequest(client, headers);
                Assert.Fail("Should throw an exception");
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException as DocumentClientException;
                Assert.AreEqual(HttpStatusCode.BadRequest, innerException.StatusCode, $"Invalid status code: {innerException}");
            }

            headers = new Documents.Collections.RequestNameValueCollection
            {
                { "pageSize", "\"Invalid header type\"" }
            };

            try
            {
                ReadFeedScript(client, headers);
                Assert.Fail("Should throw an exception");
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException as DocumentClientException;
                Assert.AreEqual(HttpStatusCode.BadRequest, innerException.StatusCode, $"Invalid status code: {innerException}");
            }

            // Invalid value
            headers = new Documents.Collections.RequestNameValueCollection
            {
                { HttpConstants.HttpHeaders.PageSize, "-2" }
            };

            try
            {
                ReadDatabaseFeedRequest(client, headers);
                Assert.Fail("Should throw an exception");
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException as DocumentClientException;
                Assert.AreEqual(HttpStatusCode.BadRequest, innerException.StatusCode, $"Invalid status code: {innerException}");
            }

            headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.PageSize, Int64.MaxValue.ToString(CultureInfo.InvariantCulture));

            try
            {
                ReadFeedScript(client, headers);
                Assert.Fail("Should throw an exception");
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException as DocumentClientException;
                Assert.AreEqual(HttpStatusCode.BadRequest, innerException.StatusCode, $"Invalid status code: {innerException}");
            }

            // Valid page size
            headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.PageSize, "20");
            var response = ReadDatabaseFeedRequest(client, headers);
            Assert.IsTrue(response.StatusCode == HttpStatusCode.OK);

            headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add("pageSize", "20");
            var result = ReadFeedScript(client, headers);
            Assert.IsTrue(result.StatusCode == HttpStatusCode.OK);

            // dynamic page size
            headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.PageSize, "-1");
            response = ReadDatabaseFeedRequest(client, headers);
            Assert.IsTrue(response.StatusCode == HttpStatusCode.OK);

            headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add("pageSize", "-1");
            result = ReadFeedScript(client, headers);
            Assert.IsTrue(result.StatusCode == HttpStatusCode.OK);
        }

        [TestMethod]
        public async Task ValidateConsistencyLevelGateway()
        {
            DocumentClient client = TestCommon.CreateClient(true);
            await ValidateConsistencyLevel(client);
        }

        [TestMethod]
        public async Task ValidateConsistencyLevelRntbd()
        {
            DocumentClient client = TestCommon.CreateClient(false, Protocol.Tcp);
            await ValidateConsistencyLevel(client);
        }

        private async Task ValidateConsistencyLevel(DocumentClient client)
        {
            DocumentCollection collection = TestCommon.CreateOrGetDocumentCollection(client);

            // Value not supported
            RequestNameValueCollection headers = new();
            headers.Add(HttpConstants.HttpHeaders.ConsistencyLevel, "Not a valid value");

            try
            {
                await ReadDocumentFeedRequestAsync(client, collection.ResourceId, headers);
                Assert.Fail("Should throw an exception");
            }
            catch (Exception ex) when (ex.GetBaseException() is DocumentClientException)
            {
                DocumentClientException documentClientException = ex.GetBaseException() as DocumentClientException;
                Assert.IsTrue(
                    documentClientException.StatusCode == HttpStatusCode.BadRequest,
                    "invalid status code");
            }

            // Supported value
            headers = new RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.ConsistencyLevel, ConsistencyLevel.Eventual.ToString());
            var response = ReadDocumentFeedRequestAsync(client, collection.ResourceId, headers).Result;
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Invalid status code");
        }

        [TestMethod]
        public void ValidateJsonSerializationFormatGateway()
        {
            using var client = TestCommon.CreateClient(true);
            ValidateJsonSerializationFormat(client);
        }

        [TestMethod]
        public void ValidateJsonSerializationFormatRntbd()
        {
            using var client = TestCommon.CreateClient(false, Protocol.Tcp);
            ValidateJsonSerializationFormat(client);
        }

        private void ValidateJsonSerializationFormat(DocumentClient client)
        {
            DocumentCollection collection = TestCommon.CreateOrGetDocumentCollection(client);
            this.ValidateJsonSerializationFormatReadFeed(client, collection);
            this.ValidateJsonSerializationFormatQuery(client, collection);
        }

        private void ValidateJsonSerializationFormatReadFeed(DocumentClient client, DocumentCollection collection)
        {
            // Value not supported
            INameValueCollection headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.ContentSerializationFormat, "Not a valid value");

            try
            {
                ReadDocumentFeedRequestSinglePartition(client, collection.ResourceId, headers);
                Assert.Fail("Should throw an exception");
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException as DocumentClientException;
                Assert.AreEqual(HttpStatusCode.BadRequest, innerException.StatusCode, $"invalid status code: {innerException}");
            }

            // Supported values

            // Text
            headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.ContentSerializationFormat, ContentSerializationFormat.JsonText.ToString());
            var response = ReadDocumentFeedRequestAsync(client, collection.ResourceId, headers).Result;
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Invalid status code");
            Assert.IsTrue(response.ResponseBody.ReadByte() < HeadersValidationTests.BinarySerializationByteMarkValue);

            // None
            headers = new Documents.Collections.RequestNameValueCollection();
            response = ReadDocumentFeedRequestAsync(client, collection.ResourceId, headers).Result;
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Invalid status code");
            Assert.IsTrue(response.ResponseBody.ReadByte() < HeadersValidationTests.BinarySerializationByteMarkValue);

            // Binary (Read feed should ignore all options)
            headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.ContentSerializationFormat, ContentSerializationFormat.CosmosBinary.ToString());
            response = ReadDocumentFeedRequestAsync(client, collection.ResourceId, headers).Result;
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Invalid status code");
            Assert.AreEqual((int)JsonSerializationFormat.Binary, response.ResponseBody.ReadByte());
        }

        private void ValidateJsonSerializationFormatQuery(DocumentClient client, DocumentCollection collection)
        {
            SqlQuerySpec sqlQuerySpec = new SqlQuerySpec("SELECT * FROM c");
            // Value not supported
            INameValueCollection headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.ContentSerializationFormat, "Not a valid value");

            try
            {
                QueryRequest(client, collection.ResourceId, sqlQuerySpec, headers);
                Assert.Fail("Should throw an exception");
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException as DocumentClientException;
                Assert.AreEqual(HttpStatusCode.BadRequest, innerException.StatusCode, $"Invalid status code: {innerException}");
            }

            // Supported values

            // Text
            headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.ContentSerializationFormat, ContentSerializationFormat.JsonText.ToString());
            var response = QueryRequest(client, collection.ResourceId, sqlQuerySpec, headers);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Invalid status code");
            Assert.IsTrue(response.ResponseBody.ReadByte() < HeadersValidationTests.BinarySerializationByteMarkValue);

            // None
            headers = new Documents.Collections.RequestNameValueCollection();
            response = QueryRequest(client, collection.ResourceId, sqlQuerySpec, headers);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Invalid status code");
            Assert.IsTrue(response.ResponseBody.ReadByte() < HeadersValidationTests.BinarySerializationByteMarkValue);

            // Binary
            headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.ContentSerializationFormat, ContentSerializationFormat.CosmosBinary.ToString());
            response = QueryRequest(client, collection.ResourceId, sqlQuerySpec, headers);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Invalid status code");
            Assert.IsTrue(response.ResponseBody.ReadByte() == HeadersValidationTests.BinarySerializationByteMarkValue);
        }

        [TestMethod]
        public void ValidateSupportedSerializationFormatsGateway()
        {
            using var client = TestCommon.CreateClient(true);
            this.ValidateSupportedSerializationFormats(client, true);
        }

        [TestMethod]
        public void ValidateSupportedSerializationFormatsRntbd()
        {
            using var client = TestCommon.CreateClient(false, Protocol.Tcp);
            this.ValidateSupportedSerializationFormats(client, false);
        }

        private void ValidateSupportedSerializationFormats(DocumentClient client, bool isHttps)
        {
            DocumentCollection collection = TestCommon.CreateOrGetDocumentCollection(client);
            this.ValidateSupportedSerializationFormatsReadFeed(client, collection, isHttps);

            List<SqlQuerySpec> sqlQueryList = new List<SqlQuerySpec>()
            {
                new SqlQuerySpec("SELECT * FROM c"),
                new SqlQuerySpec("SELECT c.id FROM c ORDER BY c.partitionKey"),
                new SqlQuerySpec("SELECT c.name FROM c GROUP BY c.name")
            };

            foreach(SqlQuerySpec sqlQuery in sqlQueryList)
            {
                this.ValidateSupportedSerializationFormatsQuery(client, collection, sqlQuery, isHttps);
            }
        }

        private void SupportedSerializationFormatsNegativeCases(
            DocumentClient client, 
            DocumentCollection collection, 
            string invalidValue,
            bool isHttps,
            SqlQuerySpec sqlQuerySpec = null)
        {
            INameValueCollection headers = new RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.SupportedSerializationFormats, invalidValue);

            try
            {
                DocumentServiceResponse response;
                if (sqlQuerySpec != null)
                {
                    response = this.QueryRequest(client, collection.ResourceId, sqlQuerySpec, headers);
                }
                else
                {
                    headers.Set(HttpConstants.HttpHeaders.PartitionKey, "[\"test\"]");
                    response = this.ReadDocumentFeedRequestSinglePartition(client, collection.ResourceId, headers);
                }

                if (isHttps)
                {
                    // Invalid value is treated as default JsonText if HTTPS
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Invalid status code");
                    Assert.AreEqual(JsonSerializationFormat.Text, this.CheckSerializationFormat(response));
                }
                else
                {
                    Assert.Fail("Should throw an exception");
                }
            }
            catch (Exception ex)
            {
                DocumentClientException innerException = ex.InnerException as DocumentClientException;
                Assert.AreEqual(HttpStatusCode.BadRequest, innerException.StatusCode, $"invalid status code {innerException}");
            }
        }

        private void SupportedSerializationFormatsPositiveCases(
            DocumentClient client,
            DocumentCollection collection,
            SupportedSerializationFormats expectedFormat,
            string supportedSerializationFormats,
            SqlQuerySpec sqlQuerySpec = null)
        {
            INameValueCollection headers = new RequestNameValueCollection();

            headers.Add(HttpConstants.HttpHeaders.SupportedSerializationFormats, supportedSerializationFormats);
            DocumentServiceResponse response;
            if(sqlQuerySpec!=null)
            {
                response = this.QueryRequest(client, collection.ResourceId, sqlQuerySpec, headers);
            }
            else
            {
                Assert.IsTrue(expectedFormat == SupportedSerializationFormats.JsonText, "ReadFeed response should be in Text");
                response = this.ReadDocumentFeedRequestAsync(client, collection.ResourceId, headers).Result;
            }

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Invalid status code");
            
            if(expectedFormat == SupportedSerializationFormats.JsonText)
            {
                Assert.IsTrue(this.CheckSerializationFormat(response) == JsonSerializationFormat.Text);
            }
            else
            {
                Assert.IsTrue(this.CheckSerializationFormat(response) == JsonSerializationFormat.Binary);
            }
        }

        private void ValidateSupportedSerializationFormatsReadFeed(DocumentClient client, DocumentCollection collection, bool isHttps)
        {
            // Value not supported
            this.SupportedSerializationFormatsNegativeCases(client, collection, "Invalid value", isHttps);

            // Supported values
            this.SupportedSerializationFormatsPositiveCases(client, collection, expectedFormat: SupportedSerializationFormats.JsonText, supportedSerializationFormats: "JSONText");
            this.SupportedSerializationFormatsPositiveCases(client, collection, expectedFormat: SupportedSerializationFormats.JsonText, supportedSerializationFormats: "COSMOSbinary");
            this.SupportedSerializationFormatsPositiveCases(client, collection, expectedFormat: SupportedSerializationFormats.JsonText, supportedSerializationFormats: "JsonText, CosmosBinary");
        }

        private void ValidateSupportedSerializationFormatsQuery(DocumentClient client, DocumentCollection collection, SqlQuerySpec sqlQuerySpec, bool isHttps)
        {
            // Values not supported
            this.SupportedSerializationFormatsNegativeCases(client, collection, "Invalid value", isHttps, sqlQuerySpec: sqlQuerySpec);
            this.SupportedSerializationFormatsNegativeCases(client, collection, ", ,", isHttps, sqlQuerySpec: sqlQuerySpec);
            this.SupportedSerializationFormatsNegativeCases(client, collection, ",,", isHttps, sqlQuerySpec: sqlQuerySpec);
            this.SupportedSerializationFormatsNegativeCases(client, collection, "JsonText CosmosBinary", isHttps, sqlQuerySpec: sqlQuerySpec);
            this.SupportedSerializationFormatsNegativeCases(client, collection, ",JsonText|CosmosBinary", isHttps, sqlQuerySpec: sqlQuerySpec);
            this.SupportedSerializationFormatsNegativeCases(client, collection, "Json Text", isHttps, sqlQuerySpec: sqlQuerySpec);
            this.SupportedSerializationFormatsNegativeCases(client, collection, "Json,Text", isHttps, sqlQuerySpec: sqlQuerySpec);
            this.SupportedSerializationFormatsNegativeCases(client, collection, "JsonText, ", isHttps, sqlQuerySpec: sqlQuerySpec);

            // Supported values
            this.SupportedSerializationFormatsPositiveCases(client, collection,
                expectedFormat: SupportedSerializationFormats.JsonText,
                supportedSerializationFormats: "jsontext",
                sqlQuerySpec: sqlQuerySpec);
            this.SupportedSerializationFormatsPositiveCases(client, collection,
                expectedFormat: SupportedSerializationFormats.CosmosBinary,
                supportedSerializationFormats: "COSMOSBINARY",
                sqlQuerySpec: sqlQuerySpec);
            this.SupportedSerializationFormatsPositiveCases(client, collection,
                expectedFormat: SupportedSerializationFormats.CosmosBinary,
                supportedSerializationFormats: "JsonText, CosmosBinary",
                sqlQuerySpec: sqlQuerySpec);
            this.SupportedSerializationFormatsPositiveCases(client, collection,
                expectedFormat: SupportedSerializationFormats.CosmosBinary,
                supportedSerializationFormats: "CosmosBinary, HybridRow",
                sqlQuerySpec: sqlQuerySpec);
            this.SupportedSerializationFormatsPositiveCases(client, collection,
                expectedFormat: SupportedSerializationFormats.CosmosBinary,
                supportedSerializationFormats: "JsonText, CosmosBinary, HybridRow",
                sqlQuerySpec: sqlQuerySpec);
            this.SupportedSerializationFormatsPositiveCases(client, collection,
                expectedFormat: SupportedSerializationFormats.CosmosBinary,
                supportedSerializationFormats: "JsonText, CosmosBinary, HybridRow",
                sqlQuerySpec: sqlQuerySpec);
            this.SupportedSerializationFormatsPositiveCases(client, collection,
                expectedFormat: SupportedSerializationFormats.CosmosBinary,
                supportedSerializationFormats: "JsonText, CosmosBinary, HybridRow",
                sqlQuerySpec: sqlQuerySpec);
        }

        private JsonSerializationFormat CheckSerializationFormat(DocumentServiceResponse response)
        {
            if(response.ResponseBody.ReadByte() < BinarySerializationByteMarkValue)
            {
                return JsonSerializationFormat.Text;
            }
            else
            {
                return JsonSerializationFormat.Binary;
            }
        }

        [TestMethod]
        public void ValidateIndexingDirectiveGateway()
        {
            var client = TestCommon.CreateClient(true);
            ValidateIndexingDirective(client);
        }
        [TestMethod]
        public void ValidateIndexingDirectiveRntbd()
        {
            //var client = TestCommon.CreateClient(false, Protocol.Tcp);
            var client = TestCommon.CreateClient(true, Protocol.Tcp);
            ValidateIndexingDirective(client);
        }

        private void ValidateIndexingDirective(DocumentClient client)
        {
            // Number out of range.
            INameValueCollection headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.IndexingDirective, "\"Invalid Value\"");

            try
            {
                CreateDocumentRequest(client, headers);
                Assert.Fail("Should throw an exception");
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException as DocumentClientException;
                Assert.AreEqual(HttpStatusCode.BadRequest, innerException.StatusCode, $"invalid status code {innerException}");
            }

            headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add("indexAction", "\"Invalid Value\"");

            try
            {
                CreateDocumentScript(client, headers);
                Assert.Fail("Should throw an exception");
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException as DocumentClientException;
                Assert.AreEqual(HttpStatusCode.BadRequest, innerException.StatusCode, $"invalid status code {innerException}");
            }

            // Valid Indexing Directive
            headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.IndexingDirective, IndexingDirective.Exclude.ToString());
            var response = CreateDocumentRequest(client, headers);
            Assert.IsTrue(response.StatusCode == HttpStatusCode.Created);

            headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add("indexAction", "\"exclude\"");
            var result = CreateDocumentScript(client, headers);
            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode, "Invalid status code");
        }

        [TestMethod]
        public void ValidateEnableScanInQueryGateway()
        {
            var client = TestCommon.CreateClient(true);
            ValidateEnableScanInQuery(client);
        }

        [TestMethod]
        public void ValidateEnableScanInQueryRntbd()
        {
            var client = TestCommon.CreateClient(false, Protocol.Tcp);
            ValidateEnableScanInQuery(client);
        }

        private void ValidateEnableScanInQuery(DocumentClient client, bool isHttps = false)
        {
            // Value not boolean
            INameValueCollection headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.EnableScanInQuery, "Not a boolean");

            try
            {
                var response = ReadDatabaseFeedRequest(client, headers);
                if (isHttps)
                {
                    Assert.Fail("Should throw an exception");
                }
                else
                {
                    // Invalid boolean is treated as false by TCP
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Invalid status code");
                }
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException as DocumentClientException;
                Assert.AreEqual(HttpStatusCode.BadRequest, innerException.StatusCode, $"invalid status code {innerException}");
            }

            // Valid boolean
            headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.EnableScanInQuery, "true");
            var response2 = ReadDatabaseFeedRequest(client, headers);
            Assert.AreEqual(HttpStatusCode.OK, response2.StatusCode, "Invalid status code");
        }

        [TestMethod]
        public void ValidateEnableLowPrecisionOrderByGateway()
        {
            var client = TestCommon.CreateClient(true);
            ValidateEnableLowPrecisionOrderBy(client);
        }

        [TestMethod]
        public void ValidateEnableLowPrecisionOrderByRntbd()
        {
            var client = TestCommon.CreateClient(false, Protocol.Tcp);
            ValidateEnableLowPrecisionOrderBy(client);
        }

        private void ValidateEnableLowPrecisionOrderBy(DocumentClient client, bool isHttps = false)
        {
            // Value not boolean
            INameValueCollection headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy, "Not a boolean");

            var document = CreateDocumentRequest(client, new Documents.Collections.RequestNameValueCollection()).GetResource<Document>();
            try
            {
                var response = ReadDocumentRequest(client, document, headers);
                if (isHttps)
                {
                    Assert.Fail("Should throw an exception");
                }
                else
                {
                    // Invalid boolean is treated as false by TCP"
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Invalid status code");
                }
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException as DocumentClientException;
                Assert.AreEqual(HttpStatusCode.BadRequest, innerException.StatusCode, "Invalid status code");
            }

            // Valid boolean
            document = CreateDocumentRequest(client, new Documents.Collections.RequestNameValueCollection()).GetResource<Document>();
            headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy, "true");
            var response2 = ReadDocumentRequest(client, document, headers);
            Assert.AreEqual(HttpStatusCode.OK, response2.StatusCode, "Invalid status code");
        }

        [TestMethod]
        public void ValidateEmitVerboseTracesInQueryGateway()
        {
            var client = TestCommon.CreateClient(true);
            ValidateEmitVerboseTracesInQuery(client);
        }

        [TestMethod]
        public void ValidateEmitVerboseTracesInQueryRntbd()
        {
            var client = TestCommon.CreateClient(false, Protocol.Tcp);
            ValidateEmitVerboseTracesInQuery(client);
        }

        private void ValidateEmitVerboseTracesInQuery(DocumentClient client, bool isHttps = false)
        {
            // Value not boolean
            INameValueCollection headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.EmitVerboseTracesInQuery, "Not a boolean");

            try
            {
                var response = ReadDatabaseFeedRequest(client, headers);
                if (isHttps)
                {
                    Assert.Fail("Should throw an exception");
                }
                else
                {
                    // Invalid boolean is treated as false by TCP
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Invalid status code");
                }
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException as DocumentClientException;
                Assert.AreEqual(HttpStatusCode.BadRequest, innerException.StatusCode, $"invalid status code {innerException}");
            }

            // Valid boolean
            headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.EmitVerboseTracesInQuery, "true");
            var response2 = ReadDatabaseFeedRequest(client, headers);
            Assert.AreEqual(HttpStatusCode.OK, response2.StatusCode, "Invalid status code");
        }

        [TestMethod]
        public void ValidateIfNonMatchGateway()
        {
            using var client = TestCommon.CreateClient(true);
            ValidateIfNonMatch(client);

        }

        [TestMethod]
        public void ValidateIfNonMatchRntbd()
        {
            using var client = TestCommon.CreateClient(false, Protocol.Tcp);
            ValidateIfNonMatch(client);
        }

        [TestMethod]
        public void ValidateCustomUserAgentHeader()
        {
            const string suffix = " MyCustomUserAgent/1.0";
            ConnectionPolicy policy = new ConnectionPolicy();
            policy.UserAgentSuffix = suffix;

            string expectedUserAgent = new Cosmos.UserAgentContainer(clientId: 0).BaseUserAgent + suffix;
            string actualUserAgent = policy.UserAgentContainer.UserAgent;
            int startIndexOfClientCounter = expectedUserAgent.IndexOfNth('|', 2) + 1;

            // V3 SDK has a client counter in the user agent. This removes the count so the user agent string match.
            string expectedUserAgentNoClientCounter = expectedUserAgent.Remove(startIndexOfClientCounter, 2);
            string actualUserAgentNoClientCounter = actualUserAgent.Remove(startIndexOfClientCounter, 2);
            Assert.AreEqual(expectedUserAgentNoClientCounter, actualUserAgentNoClientCounter);

            byte[] expectedUserAgentUTF8 = Encoding.UTF8.GetBytes(expectedUserAgent);
            CollectionAssert.AreEqual(expectedUserAgentUTF8, policy.UserAgentContainer.UserAgentUTF8);
        }

        [TestMethod]
        public void ValidateCustomUserAgentContainerHeader()
        {
            const string suffix = " MyCustomUserAgent/1.0";
            UserAgentContainer userAgentContainer = new TestUserAgentContainer();
            Assert.AreEqual("TestUserAgentContainer.BaseUserAgent", userAgentContainer.BaseUserAgent);
            Assert.AreEqual("TestUserAgentContainer.BaseUserAgent", userAgentContainer.UserAgent);
            byte[] expectedUserAgentUTF8 = Encoding.UTF8.GetBytes("TestUserAgentContainer.BaseUserAgent");
            CollectionAssert.AreEqual(expectedUserAgentUTF8, userAgentContainer.UserAgentUTF8);

            userAgentContainer.Suffix = suffix;
            var expectedUserAgent = new TestUserAgentContainer().BaseUserAgent + suffix;
            Assert.AreEqual(expectedUserAgent, userAgentContainer.UserAgent);

            expectedUserAgentUTF8 = Encoding.UTF8.GetBytes(expectedUserAgent);
            CollectionAssert.AreEqual(expectedUserAgentUTF8, userAgentContainer.UserAgentUTF8);
        }

        [TestMethod]
        public async Task ValidateVersionHeader()
        {
            string correctVersion = HttpConstants.Versions.CurrentVersion;
            Database db = null;
            try
            {
                DocumentClient client = TestCommon.CreateClient(true);
                db = (await client.CreateDatabaseAsync(new Database() { Id = Guid.NewGuid().ToString() })).Resource;
                PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/pk" }), Kind = PartitionKind.Hash };
                var coll = (await client.CreateDocumentCollectionAsync(db.SelfLink, new DocumentCollection() { Id = Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition })).Resource;
                var doc = (await client.CreateDocumentAsync(coll.SelfLink, new Document())).Resource;
                client.Dispose();
                client = TestCommon.CreateClient(true);
                doc = (await client.CreateDocumentAsync(coll.SelfLink, new Document())).Resource;
                HttpConstants.Versions.CurrentVersion = "2015-01-01";
                client.Dispose();
                client = TestCommon.CreateClient(true);
                try
                {
                    doc = (await client.CreateDocumentAsync(coll.SelfLink, new Document())).Resource;
                    Assert.Fail("Should have faild because of version error");
                }
                catch (CosmosException dce)
                {
                    Assert.AreEqual(dce.StatusCode, HttpStatusCode.BadRequest);
                }
            }
            finally
            {
                HttpConstants.Versions.CurrentVersion = correctVersion;
                using DocumentClient client = TestCommon.CreateClient(true);
                await client.DeleteDatabaseAsync(db);
            }
        }

        [TestMethod]
        public async Task ValidateCurrentWriteQuorumAndReplicaSetHeader()
        {
            using CosmosClient client = TestCommon.CreateCosmosClient(false);
            Cosmos.Database db = null;
            try
            {
                db = await client.CreateDatabaseAsync(Guid.NewGuid().ToString());
                PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
                ContainerProperties containerSetting = new ContainerProperties()
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKey = partitionKeyDefinition
                };
                Container coll = await db.CreateContainerAsync(containerSetting);
                Document documentDefinition = new Document { Id = Guid.NewGuid().ToString() };
                ItemResponse<Document> docResult = await coll.CreateItemAsync<Document>(documentDefinition);
                Assert.IsTrue(int.Parse(docResult.Headers[WFConstants.BackendHeaders.CurrentWriteQuorum], CultureInfo.InvariantCulture) > 0);
                Assert.IsTrue(int.Parse(docResult.Headers[WFConstants.BackendHeaders.CurrentReplicaSetSize], CultureInfo.InvariantCulture) > 0);
            }
            finally
            {
                await db.DeleteAsync();
            }
        }

        [TestMethod]
        [Ignore]
        [TestCategory("Ignore") /* Used to filter out ignored tests in lab runs */]
        public void ValidateGlobalCompltedLSNAndNumberOfReadRegionsHeader()
        {
            using DocumentClient client = TestCommon.CreateClient(false);
            Database db = null;
            try
            {
                var dbResource = client.CreateDatabaseAsync(new Database() { Id = Guid.NewGuid().ToString() }).Result;
                db = dbResource.Resource;
                var coll = client.CreateDocumentCollectionAsync(db, new DocumentCollection() { Id = Guid.NewGuid().ToString() }).Result.Resource;
                var docResult = client.CreateDocumentAsync(coll, new Document() { Id = Guid.NewGuid().ToString() }).Result;
                long nCurrentGlobalCommittedLSN = -1;
                long nNumberOfReadRegions = 0;
                for (uint i = 0; i < 3; i++)
                {
                    client.LockClient(i);
                    var readResult = client.ReadDocumentAsync(docResult.Resource).Result;
                    nCurrentGlobalCommittedLSN = long.Parse(readResult.ResponseHeaders[WFConstants.BackendHeaders.GlobalCommittedLSN], CultureInfo.InvariantCulture);
                    nNumberOfReadRegions = long.Parse(readResult.ResponseHeaders[WFConstants.BackendHeaders.NumberOfReadRegions], CultureInfo.InvariantCulture);

                    Assert.IsTrue(nCurrentGlobalCommittedLSN >= 0);
                    Assert.IsTrue(nNumberOfReadRegions >= 0);
                }
            }
            finally
            {
                client.DeleteDatabaseAsync(db).Wait();
            }
        }

        [TestMethod]
        public async Task ValidateCollectionIndexProgressHeaders()
        {
            using (var client = TestCommon.CreateClient(true))
            {
                await ValidateCollectionIndexProgressHeadersAsync(client, isElasticCollection: true);
            }

            using (var client = TestCommon.CreateClient(false, Protocol.Tcp))
            {
                await ValidateCollectionIndexProgressHeadersAsync(client, isElasticCollection: true);
            }
        }

        [TestMethod]
        public async Task ValidateExcludeSystemProperties()
        {
            using var client = TestCommon.CreateClient(true);
            await ValidateExcludeSystemProperties(client);
        }

        private async Task ValidateExcludeSystemProperties(DocumentClient client)
        {
            var db = client.CreateDatabaseAsync(new Database() { Id = Guid.NewGuid().ToString() }).Result.Resource;
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            var coll = (await client.CreateDocumentCollectionAsync(db.SelfLink, new DocumentCollection() { Id = Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition })).Resource;

            //CASE 1. insert document with system properties excluded
            Document doc1 = await client.CreateDocumentAsync(
                coll.SelfLink,
                new Document { Id = "doc1" },
                new RequestOptions { ExcludeSystemProperties = true });

            bool bHasAttachments = doc1.ToString().Contains("attachments");
            Assert.IsFalse(bHasAttachments);
            bool bHasSelfLink = doc1.ToString().Contains("_self");
            Assert.IsFalse(bHasSelfLink);

            //read document and ask system properties to be included
            Document readDoc1WithProps = await client.ReadDocumentAsync(
                coll.AltLink + "/docs/doc1",
                new RequestOptions { ExcludeSystemProperties = false, PartitionKey = new PartitionKey("doc1") });

            bHasAttachments = readDoc1WithProps.ToString().Contains("attachments");
            Assert.IsTrue(bHasAttachments);
            bHasSelfLink = readDoc1WithProps.ToString().Contains("_self");
            Assert.IsTrue(bHasSelfLink);

            //read document and explicitly exclude system properties
            Document readDoc1WoutProps = await client.ReadDocumentAsync(
                coll.AltLink + "/docs/doc1",
                new RequestOptions { ExcludeSystemProperties = true, PartitionKey = new PartitionKey("doc1") });

            bHasAttachments = readDoc1WoutProps.ToString().Contains("attachments");
            Assert.IsFalse(bHasAttachments);
            bHasSelfLink = readDoc1WoutProps.ToString().Contains("_self");
            Assert.IsFalse(bHasSelfLink);

            //read document with default settings (system properties should be included)
            Document readDoc1Default = await client.ReadDocumentAsync(coll.AltLink + "/docs/doc1", new RequestOptions() { PartitionKey = new PartitionKey("doc1") });
            Assert.AreEqual(readDoc1WithProps.ToString(), readDoc1Default.ToString());


            //CASE 2. insert document and explicitly include system properties
            Document doc2 = await client.CreateDocumentAsync(
                coll.SelfLink,
                new Document { Id = "doc2" },
                new RequestOptions { ExcludeSystemProperties = false });

            bHasAttachments = doc2.ToString().Contains("attachments");
            Assert.IsTrue(bHasAttachments);
            bHasSelfLink = doc2.ToString().Contains("_self");
            Assert.IsTrue(bHasSelfLink);

            //read document and ask system properties to be included
            Document readDoc2WithProps = await client.ReadDocumentAsync(
               coll.AltLink + "/docs/doc2",
               new RequestOptions { ExcludeSystemProperties = false, PartitionKey = new PartitionKey("doc2") });

            bHasAttachments = readDoc2WithProps.ToString().Contains("attachments");
            Assert.IsTrue(bHasAttachments);
            bHasSelfLink = readDoc2WithProps.ToString().Contains("_self");
            Assert.IsTrue(bHasSelfLink);

            //read document and explicitly exclude system properties, they should be still present in this case.
            Document readDoc2WoutProps = await client.ReadDocumentAsync(
                coll.AltLink + "/docs/doc2",
                new RequestOptions { ExcludeSystemProperties = true, PartitionKey = new PartitionKey("doc2") });

            bHasAttachments = readDoc2WoutProps.ToString().Contains("attachments");
            Assert.IsTrue(bHasAttachments);
            bHasSelfLink = readDoc2WoutProps.ToString().Contains("_self");
            Assert.IsTrue(bHasSelfLink);

            //read document with default settings (system properties should be included)
            Document readDoc2Default = await client.ReadDocumentAsync(coll.AltLink + "/docs/doc2", new RequestOptions() { PartitionKey = new PartitionKey("doc2") });
            Assert.AreEqual(readDoc2WithProps.ToString(), readDoc2Default.ToString());


            //CASE 3. insert document with default settings (system properties should be included)
            Document doc3 = await client.CreateDocumentAsync(
                coll.SelfLink,
                new Document { Id = "doc3" });

            bHasAttachments = doc3.ToString().Contains("attachments");
            Assert.IsTrue(bHasAttachments);
            bHasSelfLink = doc3.ToString().Contains("_self");
            Assert.IsTrue(bHasSelfLink);

            //read document and ask system properties to be included
            Document readDoc3WithProps = await client.ReadDocumentAsync(
               coll.AltLink + "/docs/doc3",
               new RequestOptions { ExcludeSystemProperties = false, PartitionKey = new PartitionKey("doc3") });

            bHasAttachments = readDoc3WithProps.ToString().Contains("attachments");
            Assert.IsTrue(bHasAttachments);
            bHasSelfLink = readDoc3WithProps.ToString().Contains("_self");
            Assert.IsTrue(bHasSelfLink);

            //read document and explicitly exclude system properties, they should be still present in this case.
            Document readDoc3WoutProps = await client.ReadDocumentAsync(
                coll.AltLink + "/docs/doc3",
                new RequestOptions { ExcludeSystemProperties = true, PartitionKey = new PartitionKey("doc3") });

            bHasAttachments = readDoc3WoutProps.ToString().Contains("attachments");
            Assert.IsTrue(bHasAttachments);
            bHasSelfLink = readDoc3WoutProps.ToString().Contains("_self");
            Assert.IsTrue(bHasSelfLink);

            //read document with default settings (system properties should be included)
            Document readDoc3Default = await client.ReadDocumentAsync(coll.AltLink + "/docs/doc3", new RequestOptions() { PartitionKey = new PartitionKey("doc3") });
            Assert.AreEqual(readDoc3WithProps.ToString(), readDoc3Default.ToString());

            await client.DeleteDatabaseAsync(db);
        }

        private async Task ValidateCollectionIndexProgressHeadersAsync(DocumentClient client, bool isElasticCollection)
        {
            Database db = (await client.CreateDatabaseAsync(new Database() { Id = Guid.NewGuid().ToString() })).Resource;

            try
            {
                PartitionKeyDefinition pkd = null;
                RequestOptions options = null;

                if (isElasticCollection)
                {
                    pkd = new PartitionKeyDefinition { Paths = new Collection<string> { "/id" } };
                    options = new RequestOptions { PopulateQuotaInfo = true };
                }

                var consistentCollection = new DocumentCollection() { Id = Guid.NewGuid().ToString() };
                if (isElasticCollection)
                {
                    consistentCollection.PartitionKey = pkd;
                }

                consistentCollection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
                consistentCollection = (await client.CreateDocumentCollectionAsync(db, consistentCollection)).Resource;

                var noneIndexCollection = new DocumentCollection() { Id = Guid.NewGuid().ToString() };
                if (isElasticCollection)
                {
                    noneIndexCollection.PartitionKey = pkd;
                }

                noneIndexCollection.IndexingPolicy.Automatic = false;
                noneIndexCollection.IndexingPolicy.IndexingMode = IndexingMode.None;
                noneIndexCollection = (await client.CreateDocumentCollectionAsync(db, noneIndexCollection)).Resource;

                var doc = new Document() { Id = Guid.NewGuid().ToString() };
                await client.CreateDocumentAsync(consistentCollection, doc);
                await client.CreateDocumentAsync(noneIndexCollection, doc);

                // Consistent-indexing collection.
                {
                    var collectionResponse = await client.ReadDocumentCollectionAsync(consistentCollection, options);
                    Assert.IsFalse(collectionResponse.Headers.AllKeys().Contains(HttpConstants.HttpHeaders.CollectionLazyIndexingProgress),
                        "No lazy indexer progress when reading consistent collection.");
                    Assert.AreEqual(100, int.Parse(collectionResponse.ResponseHeaders[HttpConstants.HttpHeaders.CollectionIndexTransformationProgress], CultureInfo.InvariantCulture),
                        "Expect reindexer progress when reading consistent collection.");
                    Assert.AreEqual(-1, collectionResponse.LazyIndexingProgress);
                    Assert.AreEqual(100, collectionResponse.IndexTransformationProgress);
                }

                // None-indexing collection.
                {
                    var collectionResponse = await client.ReadDocumentCollectionAsync(noneIndexCollection, options);
                    Assert.IsFalse(collectionResponse.Headers.AllKeys().Contains(HttpConstants.HttpHeaders.CollectionLazyIndexingProgress),
                        "No lazy indexer progress when reading none-index collection.");
                    Assert.AreEqual(100, int.Parse(collectionResponse.ResponseHeaders[HttpConstants.HttpHeaders.CollectionIndexTransformationProgress], CultureInfo.InvariantCulture),
                        "Expect reindexer progress when reading none-index collection.");
                    Assert.AreEqual(-1, collectionResponse.LazyIndexingProgress);
                    Assert.AreEqual(100, collectionResponse.IndexTransformationProgress);
                }

                // Consistent -> Consistent.
                for (int i = 0; i < 100; ++i)
                {
                    var document = new Document();
                    for (int k = 0; k < 100; ++k)
                    {
                        document.SetPropertyValue(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
                    }

                    await client.CreateDocumentAsync(consistentCollection, document);
                }

                consistentCollection.IndexingPolicy.IncludedPaths.Clear();
                consistentCollection.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/*" });
                consistentCollection = await client.ReplaceDocumentCollectionAsync(consistentCollection);
                await Util.WaitForReIndexingToFinish(300, consistentCollection);

                if (isElasticCollection)
                {
                    foreach (var collection in new DocumentCollection[] { consistentCollection, noneIndexCollection })
                    {
                        // Do not expect index progress headers for elastic collection when the PopulateQuotaInfo header is not specified.
                        var collectionResponse = await client.ReadDocumentCollectionAsync(collection);
                        Assert.IsNull(collectionResponse.ResponseHeaders[HttpConstants.HttpHeaders.CollectionIndexTransformationProgress]);
                        Assert.IsNull(collectionResponse.ResponseHeaders[HttpConstants.HttpHeaders.CollectionLazyIndexingProgress]);
                        Assert.AreEqual(-1, collectionResponse.LazyIndexingProgress);
                        Assert.AreEqual(-1, collectionResponse.IndexTransformationProgress);
                    }
                }
            }
            finally
            {
                await client.DeleteDatabaseAsync(db);
            }
        }

        private void ValidateIfNonMatch(DocumentClient client)
        {
            // Valid if-match
            var document = CreateDocumentRequest(client, new Documents.Collections.RequestNameValueCollection()).GetResource<Document>();
            var headers = new Documents.Collections.RequestNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.IfNoneMatch, document.ETag);
            var response = ReadDocumentRequest(client, document, headers);
            Assert.AreEqual(HttpStatusCode.NotModified, response.StatusCode, "Invalid status code");

            // validateInvalidIfMatch
            AccessCondition condition = new AccessCondition() { Type = AccessConditionType.IfMatch, Condition = "invalid etag" };
            try
            {
                var replacedDoc = client.ReplaceDocumentAsync(document.SelfLink, document, new RequestOptions() { AccessCondition = condition }).Result.Resource;
                Assert.Fail("should not reach here");
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException as DocumentClientException;
                Assert.AreEqual(HttpStatusCode.PreconditionFailed, innerException.StatusCode, $"invalid status code {innerException}");
            }
        }

        private DocumentServiceResponse QueryRequest(DocumentClient client, string collectionId, SqlQuerySpec sqlQuerySpec, INameValueCollection headers)
        {
            headers.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
            headers.Add(HttpConstants.HttpHeaders.ContentType, RuntimeConstants.MediaTypes.QueryJson);
            DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Query, collectionId, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, headers);

            Range<string> fullRange = new Range<string>(
                PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                true,
                false);
            IRoutingMapProvider routingMapProvider = client.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton).Result;
            IReadOnlyList<PartitionKeyRange> ranges = routingMapProvider.TryGetOverlappingRangesAsync(collectionId, fullRange, NoOpTrace.Singleton).Result;
            request.RouteTo(new PartitionKeyRangeIdentity(collectionId, ranges.First().Id));

            string queryText = JsonConvert.SerializeObject(sqlQuerySpec);
            request.Body = new MemoryStream(Encoding.UTF8.GetBytes(queryText));

            var response = client.ExecuteQueryAsync(request, null).Result;
            return response;
        }

        private Task<DocumentServiceResponse> ReadDocumentFeedRequestAsync(DocumentClient client, string collectionId, INameValueCollection headers)
        {
            DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.ReadFeed, collectionId, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, headers);

            Range<string> fullRange = new Range<string>(
                PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                true,
                false);
            IRoutingMapProvider routingMapProvider = client.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton).Result;
            IReadOnlyList<PartitionKeyRange> ranges = routingMapProvider.TryGetOverlappingRangesAsync(collectionId, fullRange, NoOpTrace.Singleton).Result;
            request.RouteTo(new PartitionKeyRangeIdentity(collectionId, ranges.First().Id));

            Task<DocumentServiceResponse> response = client.ReadFeedAsync(request, retryPolicy: null);
            return response;
        }

        private Task<DocumentServiceResponse> ReadDocumentChangeFeedRequestAsync(
            DocumentClient client,
            string collectionId,
            TimeSpan? maxPollingInterval)
        {
            if (client == null || collectionId == null || collectionId == "")
            {
                Assert.Fail("null client or collectionId");
            }

            RequestNameValueCollection headers = new RequestNameValueCollection();
            headers.Set(
                HttpConstants.HttpHeaders.A_IM,
                HttpConstants.A_IMHeaderValues.IncrementalFeed);
            if (maxPollingInterval.HasValue)
            {
                headers.Set(
                    HttpConstants.HttpHeaders.MaxPollingIntervalMilliseconds,
                    maxPollingInterval.Value.TotalMilliseconds.ToString());
            }

            return ReadDocumentFeedRequestAsync(client, collectionId, headers);
        }

        private DocumentServiceResponse ReadDocumentFeedRequestSinglePartition(DocumentClient client, string collectionId, INameValueCollection headers)
        {
            DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.ReadFeed, collectionId, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, headers);
            var response = client.ReadFeedAsync(request, null).Result;
            return response;
        }

        private DocumentServiceResponse ReadDatabaseFeedRequest(DocumentClient client, INameValueCollection headers)
        {
            DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.ReadFeed, null, ResourceType.Database, AuthorizationTokenType.PrimaryMasterKey, headers);
            var response = client.ReadFeedAsync(request, null).Result;
            return response;
        }

        private StoredProcedureResponse<string> ReadFeedScript(DocumentClient client, INameValueCollection headers)
        {
            var headersIterator = headers.AllKeys().SelectMany(headers.GetValues, (k, v) => new { key = k, value = v });
            var scriptOptions = "{";
            var headerIndex = 0;
            foreach (var header in headersIterator)
            {
                if (headerIndex != 0)
                {
                    scriptOptions += ", ";
                }

                headerIndex++;
                scriptOptions += header.key + ":" + header.value;
            }

            scriptOptions += "}";

            var script = @"function() {
                var client = getContext().getCollection();
                function callback(err, docFeed, responseOptions) {
                    if(err) throw 'Error while reading documents';
                    docFeed.forEach(function(doc, i, arr) { getContext().getResponse().appendBody(JSON.stringify(doc));  });
                };
                client.readDocuments(client.getSelfLink()," + scriptOptions + @", callback);}";

            Database database = client.CreateDatabaseAsync(new Database { Id = Guid.NewGuid().ToString() }).Result;
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            DocumentCollection collection = client.CreateDocumentCollectionAsync(database.SelfLink,
                new DocumentCollection
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKey = partitionKeyDefinition
                }).Result;
            var sproc = new StoredProcedure() { Id = Guid.NewGuid().ToString(), Body = script };
            var createdSproc = client.CreateStoredProcedureAsync(collection, sproc).Result.Resource;
            RequestOptions requestOptions = new RequestOptions();
            requestOptions.PartitionKey = new PartitionKey("test");
            var result = client.ExecuteStoredProcedureAsync<string>(createdSproc, requestOptions).Result;
            return result;
        }

        private DocumentServiceResponse CreateDocumentRequest(DocumentClient client, INameValueCollection headers)
        {
            Database database = client.CreateDatabaseAsync(new Database { Id = Guid.NewGuid().ToString() }).Result;
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            DocumentCollection collection = client.CreateDocumentCollectionAsync(database.SelfLink,
                new DocumentCollection
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKey = partitionKeyDefinition
                }).Result;
            var document = new Document() { Id = Guid.NewGuid().ToString() };
            DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Create, collection.SelfLink, document, ResourceType.Document, AuthorizationTokenType.Invalid, headers, SerializationFormattingPolicy.None);
            PartitionKey partitionKey = new PartitionKey(document.Id);
            request.Headers.Set(HttpConstants.HttpHeaders.PartitionKey, partitionKey.InternalKey.ToJsonString());
            var response = client.CreateAsync(request, null).Result;
            return response;
        }

        private StoredProcedureResponse<string> CreateDocumentScript(DocumentClient client, INameValueCollection headers)
        {
            var headersIterator = headers.AllKeys().SelectMany(headers.GetValues, (k, v) => new { key = k, value = v });
            var scriptOptions = "{";
            var headerIndex = 0;
            foreach (var header in headersIterator)
            {
                if (headerIndex != 0)
                {
                    scriptOptions += ", ";
                }

                headerIndex++;
                scriptOptions += header.key + ":" + header.value;
            }

            scriptOptions += "}";
            var guid = Guid.NewGuid().ToString();

            var script = @" function() {
                var client = getContext().getCollection();                
                client.createDocument(client.getSelfLink(), { id: ""TestDoc"" }," + scriptOptions + @", function(err, docCreated, options) { 
                   if(err) throw new Error('Error while creating document: ' + err.message); 
                   else {
                     getContext().getResponse().setBody(JSON.stringify(docCreated));  
                   }
                });}";

            Database database = client.CreateDatabaseAsync(new Database { Id = Guid.NewGuid().ToString() }).Result;
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            DocumentCollection collection = client.CreateDocumentCollectionAsync(database.SelfLink,
                new DocumentCollection
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKey = partitionKeyDefinition
                }).Result;
            var sproc = new StoredProcedure() { Id = Guid.NewGuid().ToString(), Body = script };
            var createdSproc = client.CreateStoredProcedureAsync(collection, sproc).Result.Resource;
            RequestOptions requestOptions = new RequestOptions();
            requestOptions.PartitionKey = new PartitionKey("TestDoc");
            var result = client.ExecuteStoredProcedureAsync<string>(createdSproc, requestOptions).Result;
            return result;
        }

        private DocumentServiceResponse ReadDocumentRequest(DocumentClient client, Document doc, INameValueCollection headers)
        {
            DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, doc.SelfLink, AuthorizationTokenType.PrimaryMasterKey, headers);
            request.Headers.Set(HttpConstants.HttpHeaders.PartitionKey, new PartitionKey(doc.Id).InternalKey.ToJsonString());
            var retrievedDocResponse = client.ReadAsync(request, null).Result;
            return retrievedDocResponse;
        }

        private class TestUserAgentContainer : UserAgentContainer
        {
            internal override string BaseUserAgent
            {
                get
                {
                    return "TestUserAgentContainer.BaseUserAgent";
                }
            }
        }
    }
}
