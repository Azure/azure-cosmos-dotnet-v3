//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using global::Azure;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Authorization;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CosmosClientTests
    {
        public const string AccountEndpoint = "https://localhost:8081/";
        public const string ConnectionString = "AccountEndpoint=https://localtestcosmos.documents.azure.com:443/;AccountKey=425Mcv8CXQqzRNCgFNjIhT424GK99CKJvASowTnq15Vt8LeahXTcN5wt3342vQ==;";

        [TestMethod]
        public async Task TestDispose()
        {
            CosmosClient cosmosClient = new CosmosClient(ConnectionString);
            Database database = cosmosClient.GetDatabase("asdf");
            Container container = cosmosClient.GetContainer("asdf", "asdf");
            TransactionalBatch batch = container.CreateTransactionalBatch(new PartitionKey("asdf"));
            batch.ReadItem("Test");

            FeedIterator<dynamic> feedIterator1 = container.GetItemQueryIterator<dynamic>();
            FeedIterator<dynamic> feedIterator2 = container.GetItemQueryIterator<dynamic>(queryText: "select * from T");
            FeedIterator<dynamic> feedIterator3 = database.GetContainerQueryIterator<dynamic>(queryText: "select * from T");

            string userAgent = cosmosClient.ClientContext.UserAgent;
            // Dispose should be idempotent 
            cosmosClient.Dispose();
            cosmosClient.Dispose();

            List<Func<Task>> validateAsync = new List<Func<Task>>()
            {
                () => cosmosClient.ReadAccountAsync(),
                () => cosmosClient.CreateDatabaseAsync("asdf"),
                () => database.CreateContainerAsync("asdf", "/pkpathasdf", 200),
                () => container.ReadItemAsync<dynamic>("asdf", new PartitionKey("test")),
                () => container.Scripts.ReadStoredProcedureAsync("asdf"),
                () => container.Scripts.ReadTriggerAsync("asdf"),
                () => container.Scripts.ReadUserDefinedFunctionAsync("asdf"),
                () => batch.ExecuteAsync(),
                () => feedIterator1.ReadNextAsync(),
                () => feedIterator2.ReadNextAsync(),
                () => feedIterator3.ReadNextAsync(),
            };

            foreach (Func<Task> asyncFunc in validateAsync)
            {
                try
                {
                    await asyncFunc();
                    Assert.Fail("Should throw ObjectDisposedException");
                }
                catch (CosmosObjectDisposedException e)
                {
                    string expectedMessage = $"Cannot access a disposed 'CosmosClient'. Follow best practices and use the CosmosClient as a singleton." +
                        $" CosmosClient was disposed at: {cosmosClient.DisposedDateTimeUtc.Value.ToString("o", CultureInfo.InvariantCulture)}; CosmosClient Endpoint: https://localtestcosmos.documents.azure.com/; Created at: {cosmosClient.ClientConfigurationTraceDatum.ClientCreatedDateTimeUtc.ToString("o", CultureInfo.InvariantCulture)}; UserAgent: {userAgent};";
                    Assert.IsTrue(e.Message.Contains(expectedMessage));
                    string diagnostics = e.Diagnostics.ToString();
                    Assert.IsNotNull(diagnostics);
                    Assert.IsFalse(diagnostics.Contains("NoOp"));
                    Assert.IsTrue(diagnostics.Contains("Client Configuration"));
                    string exceptionString = e.ToString();
                    Assert.IsTrue(exceptionString.Contains(diagnostics));
                    Assert.IsTrue(exceptionString.Contains(e.Message));
                    Assert.IsTrue(exceptionString.Contains(e.StackTrace));
                }
            }
        }

        [DataTestMethod]
        [DataRow(null, "425Mcv8CXQqzRNCgFNjIhT424GK99CKJvASowTnq15Vt8LeahXTcN5wt3342vQ==")]
        [DataRow(AccountEndpoint, null)]
        [DataRow("", "425Mcv8CXQqzRNCgFNjIhT424GK99CKJvASowTnq15Vt8LeahXTcN5wt3342vQ==")]
        [DataRow(AccountEndpoint, "")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void InvalidEndpointAndKey(string endpoint, string key)
        {
            new CosmosClient(endpoint, key);
        }

        [DataTestMethod]
        [DataRow(null, "425Mcv8CXQqzRNCgFNjIhT424GK99CKJvASowTnq15Vt8LeahXTcN5wt3342vQ==")]
        [DataRow(AccountEndpoint, null)]
        [DataRow("", "425Mcv8CXQqzRNCgFNjIhT424GK99CKJvASowTnq15Vt8LeahXTcN5wt3342vQ==")]
        [DataRow(AccountEndpoint, "")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Builder_InvalidEndpointAndKey(string endpoint, string key)
        {
            new CosmosClientBuilder(endpoint, key);
        }

        [TestMethod]
        public void Builder_InvalidEndpointAndTokenCredential()
        {
            TokenCredential tokenCredential = new Mock<TokenCredential>().Object;
            Assert.ThrowsException<ArgumentNullException>(() => new CosmosClientBuilder("", tokenCredential));
            Assert.ThrowsException<ArgumentNullException>(() => new CosmosClientBuilder(null, tokenCredential));
            Assert.ThrowsException<ArgumentNullException>(() => new CosmosClientBuilder(AccountEndpoint, tokenCredential: null));
        }

        [DataTestMethod]
        [DataRow(AccountEndpoint, "425Mcv8CXQqzRNCgFNjIhT424GK88ckJvASowTnq15Vt8LeahXTcN5wt3342vQ==")]
        public async Task InvalidKey_ExceptionFullStacktrace(string endpoint, string key)
        {
            CosmosClient client = new CosmosClient(endpoint, key);

            string sqlQueryText = "SELECT * FROM c";
            try
            {
                QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
                FeedIterator<object> queryResultSetIterator = client.GetContainer(new Guid().ToString(), new Guid().ToString()).GetItemQueryIterator<object>(queryDefinition);

                while (queryResultSetIterator.HasMoreResults)
                {
                    await queryResultSetIterator.ReadNextAsync();
                }
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.StackTrace.Contains("GatewayAccountReader.InitializeReaderAsync"), ex.StackTrace);
            }
        }

        [TestMethod]
        public void InvalidConnectionString()
        {
            Assert.ThrowsException<ArgumentException>(() => new CosmosClient(""));
            Assert.ThrowsException<ArgumentNullException>(() => new CosmosClient(null));
        }

        [TestMethod]
        public async Task ValidateAuthorizationTokenProviderTestAsync()
        {
            string authKeyValue = "MockAuthKey";
            Mock<AuthorizationTokenProvider> mockAuth = new Mock<AuthorizationTokenProvider>(MockBehavior.Strict);
            mockAuth.Setup(x => x.Dispose());
            mockAuth.Setup(x => x.AddAuthorizationHeaderAsync(
                It.IsAny<Documents.Collections.INameValueCollection>(),
                It.IsAny<Uri>(),
                It.IsAny<string>(),
                It.IsAny<Documents.AuthorizationTokenType>()))
                .Callback<Documents.Collections.INameValueCollection, Uri, string, Documents.AuthorizationTokenType>(
                (headers, uri, verb, tokenType) => headers.Add(Documents.HttpConstants.HttpHeaders.Authorization, authKeyValue))
                .Returns(() => new ValueTask());

            bool validAuth = false;
            Exception exceptionToThrow = new Exception("TestException");
            Mock<IHttpHandler> mockHttpHandler = new Mock<IHttpHandler>();
            mockHttpHandler.Setup(x => x.SendAsync(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>()))
                .Callback<HttpRequestMessage, CancellationToken>(
                (request, cancellationToken) =>
                {
                    Assert.IsTrue(request.Headers.TryGetValues(Documents.HttpConstants.HttpHeaders.Authorization, out IEnumerable<string> authValues));
                    Assert.AreEqual(authKeyValue, authValues.First());
                    validAuth = true;
                }).Throws(exceptionToThrow);

            using CosmosClient client = new CosmosClient(
                "https://localhost:8081",
                authorizationTokenProvider: mockAuth.Object,
                new CosmosClientOptions()
                {
                    HttpClientFactory = () => new HttpClient(new HttpHandlerHelper(mockHttpHandler.Object)),
                });

            Container container = client.GetContainer("Test", "MockTest");

            try
            {
                await container.ReadItemAsync<ToDoActivity>(Guid.NewGuid().ToString(), new PartitionKey(Guid.NewGuid().ToString()));
            }
            catch (Exception e) when (object.ReferenceEquals(e, exceptionToThrow))
            {
            }

            Assert.IsTrue(validAuth);
        }

        [TestMethod]
        public void Builder_InvalidConnectionString()
        {
            Assert.ThrowsException<ArgumentException>(() => new CosmosClientBuilder(""));
            Assert.ThrowsException<ArgumentNullException>(() => new CosmosClientBuilder(null));
        }

        [TestMethod]
        public void Builder_ValidateHttpFactory()
        {
            _ = new CosmosClientBuilder("<<endpoint-here>>", "<<key-here>>")
                .WithHttpClientFactory(() => new HttpClient())
                .WithConnectionModeGateway();

            // Validate that setting it to null does not throw an argument exception
            _ = new CosmosClientOptions()
            {
                HttpClientFactory = null,
                WebProxy = new Mock<IWebProxy>().Object,
            };

            _ = new CosmosClientOptions()
            {
                WebProxy = new Mock<IWebProxy>().Object,
                HttpClientFactory = null,
            };

            _ = new CosmosClientOptions()
            {
                WebProxy = null,
                HttpClientFactory = () => new HttpClient(),
            };

            _ = new CosmosClientOptions()
            {
                HttpClientFactory = () => new HttpClient(),
                WebProxy = null,
            };
        }

        [TestMethod]
        public void ValidateMasterKeyAuthProvider()
        {
            string masterKeyCredential = CosmosClientTests.NewRamdonMasterKey();

            using (CosmosClient client = new CosmosClient(
                    CosmosClientTests.AccountEndpoint,
                    masterKeyCredential))
            {
                Assert.AreEqual(typeof(AuthorizationTokenProviderMasterKey), client.AuthorizationTokenProvider.GetType());
            }
        }

        [TestMethod]
        public void ValidateResourceTokenAuthProvider()
        {
            string resourceToken = CosmosClientTests.NewRamdonResourceToken();

            using (CosmosClient client = new CosmosClient(
                    CosmosClientTests.AccountEndpoint,
                    resourceToken))
            {
                Assert.AreEqual(typeof(AuthorizationTokenProviderResourceToken), client.AuthorizationTokenProvider.GetType());
            }
        }

        [TestMethod]
        public void ValidateMasterKeyAzureCredentialAuthProvider()
        {
            string originalKey = CosmosClientTests.NewRamdonMasterKey();

            AzureKeyCredential masterKeyCredential = new AzureKeyCredential(originalKey);
            using (CosmosClient client = new CosmosClient(
                    CosmosClientTests.AccountEndpoint,
                    masterKeyCredential))
            {
                Assert.AreEqual(typeof(AzureKeyCredentialAuthorizationTokenProvider), client.AuthorizationTokenProvider.GetType());

                AzureKeyCredentialAuthorizationTokenProvider tokenProvider = (AzureKeyCredentialAuthorizationTokenProvider)client.AuthorizationTokenProvider;
                Assert.AreEqual(typeof(AuthorizationTokenProviderMasterKey), tokenProvider.authorizationTokenProvider.GetType());
            }
        }

        [TestMethod]
        public void ValidateResourceTokenAzureCredentialAuthProvider()
        {
            string resourceToken = CosmosClientTests.NewRamdonResourceToken();

            AzureKeyCredential resourceTokenCredential = new AzureKeyCredential(resourceToken);
            using (CosmosClient client = new CosmosClient(
                    CosmosClientTests.AccountEndpoint,
                    resourceTokenCredential))
            {
                Assert.AreEqual(typeof(AzureKeyCredentialAuthorizationTokenProvider), client.AuthorizationTokenProvider.GetType());

                AzureKeyCredentialAuthorizationTokenProvider tokenProvider = (AzureKeyCredentialAuthorizationTokenProvider)client.AuthorizationTokenProvider;
                Assert.AreEqual(typeof(AuthorizationTokenProviderResourceToken), tokenProvider.authorizationTokenProvider.GetType());
            }
        }

        [TestMethod]
        public async Task ValidateAzureKeyCredentialGatewayModeUpdateAsync()
        {
            const int defaultStatusCode = 401;
            const int defaultSubStatusCode = 50000;
            const int authMisMatchStatusCode = 70000;

            string originalKey = CosmosClientTests.NewRamdonMasterKey();
            string newKey = CosmosClientTests.NewRamdonResourceToken();
            string currentKey = originalKey;

            Mock<IHttpHandler> mockHttpHandler = new Mock<IHttpHandler>();
            mockHttpHandler.Setup(x => x.SendAsync(
                    It.IsAny<HttpRequestMessage>(),
                    It.IsAny<CancellationToken>()))
                .Returns((HttpRequestMessage request, CancellationToken cancellationToken) =>
                {

                    HttpResponseMessage responseMessage = new HttpResponseMessage((HttpStatusCode)defaultStatusCode);
                    if (request.RequestUri != VmMetadataApiHandler.vmMetadataEndpointUrl)
                    {
                        bool authHeaderPresent = request.Headers.TryGetValues(Documents.HttpConstants.HttpHeaders.Authorization, out IEnumerable<string> authValues);
                        Assert.IsTrue(authHeaderPresent);
                        Assert.AreNotEqual(0, authValues.Count());

                        AuthorizationHelper.GetResourceTypeAndIdOrFullName(request.RequestUri, out _, out string resourceType, out string resourceIdValue);

                        AuthorizationHelper.ParseAuthorizationToken(authValues.First(),
                            out ReadOnlyMemory<char> authType,
                            out ReadOnlyMemory<char> _,
                            out ReadOnlyMemory<char> tokenFromAuthHeader);

                        bool authValidated = MemoryExtensions.Equals(authType.Span, Documents.Constants.Properties.ResourceToken.AsSpan(), StringComparison.OrdinalIgnoreCase)
                            ? HttpUtility.UrlDecode(authValues.First()) == currentKey
                            : AuthorizationHelper.CheckPayloadUsingKey(
                                tokenFromAuthHeader,
                                request.Method.Method,
                                resourceIdValue,
                                resourceType,
                                request.Headers.Aggregate(new NameValueCollectionWrapper(), (c, kvp) => { c.Add(kvp.Key, kvp.Value); return c; }),
                                currentKey);
                        int subStatusCode = authValidated ? defaultSubStatusCode : authMisMatchStatusCode;
                        responseMessage.Headers.Add(Documents.WFConstants.BackendHeaders.SubStatus, subStatusCode.ToString());
                    }

                    return Task.FromResult(responseMessage);
                });

            AzureKeyCredential masterKeyCredential = new AzureKeyCredential(originalKey);
            using (CosmosClient client = new CosmosClient(
                    CosmosClientTests.AccountEndpoint,
                    masterKeyCredential,
                    new CosmosClientOptions()
                    {
                        HttpClientFactory = () => new HttpClient(new HttpHandlerHelper(mockHttpHandler.Object))
                    }))
            {
                Container container = client.GetContainer(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

                Func<int, int, Task> authValidation = async (int statusCode, int subStatusCode) =>
                {
                    try
                    {
                        await container.ReadItemAsync<ToDoActivity>(Guid.NewGuid().ToString(), new Cosmos.PartitionKey(Guid.NewGuid().ToString()));

                        Assert.Fail("Expected client to throw a authentication exception");
                    }
                    catch (CosmosException ex)
                    {
                        Assert.AreEqual(statusCode, (int)ex.StatusCode, ex.ToString());
                        Assert.AreEqual(subStatusCode, ex.SubStatusCode, ex.ToString());
                    }
                };

                // Key(V1)
                await authValidation(defaultStatusCode, defaultSubStatusCode);

                // Update key(V2) and let the auth validation fail 
                masterKeyCredential.Update(newKey);
                await authValidation(defaultStatusCode, authMisMatchStatusCode);

                // Updated Key(V2) and now lets succeed auth validation 
                Interlocked.Exchange(ref currentKey, newKey);
                await authValidation(defaultStatusCode, defaultSubStatusCode);
            }
        }

        private static string NewRamdonMasterKey()
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
        }

        private static string NewRamdonResourceToken()
        {
            return "type=resource&ver=1.0&sig=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
        }

        [TestMethod]
        public void CosmosClientEarlyDisposeTest()
        {
            string disposeErrorMsg = "Cannot access a disposed object";
            HashSet<string> errors = new HashSet<string>();

            void TraceHandler(string message)
            {
                if (message.Contains(disposeErrorMsg))
                {
                    errors.Add(message);
                }
            }

            DefaultTrace.TraceSource.Listeners.Add(new TestTraceListener { Callback = TraceHandler });
            DefaultTrace.InitEventListener();

            for (int z = 0; z < 100; ++z)
            {
                using CosmosClient cosmos = new(ConnectionString);
            }

            string assertMsg = String.Empty;

            foreach (string s in errors)
            {
                assertMsg += s + Environment.NewLine;
            }

            Assert.AreEqual(0, errors.Count, $"{Environment.NewLine}Errors found in trace:{Environment.NewLine}{assertMsg}");
        }

        private class TestTraceListener : TraceListener
        {
            public Action<string> Callback { get; set; }
            public override bool IsThreadSafe => true;
            public override void Write(string message)
            {
                this.Callback(message);
            }

            public override void WriteLine(string message)
            {
                this.Callback(message);
            }
        }
    }
}