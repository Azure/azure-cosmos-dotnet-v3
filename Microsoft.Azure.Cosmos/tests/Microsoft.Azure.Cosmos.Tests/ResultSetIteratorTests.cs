//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class ResultSetIteratorTests
    {
        private int? MaxItemCount { get; set; }
        private string ContinuationToken { get; set; }
        private CosmosQueryRequestOptions Options { get; set; }
        private CancellationToken CancellationToken { get; set; }
        private bool ContinueNextExecution { get; set; }

        [TestMethod]
        public async Task TestIteratorContract()
        {
            this.ContinuationToken = null;
            this.Options = new CosmosQueryRequestOptions();
            this.CancellationToken = new CancellationTokenSource().Token;
            this.ContinueNextExecution = true;

            CosmosResultSetIterator resultSetIterator = new CosmosDefaultResultSetStreamIterator(
                this.MaxItemCount,
                this.ContinuationToken,
                this.Options,
                NextResultSetDelegate);

            Assert.IsTrue(resultSetIterator.HasMoreResults );

            CosmosResponseMessage response = await resultSetIterator.FetchNextSetAsync(this.CancellationToken);
            this.ContinuationToken = response.Headers.Continuation;

            Assert.IsTrue(resultSetIterator.HasMoreResults );
            this.ContinueNextExecution = false;

            response = await resultSetIterator.FetchNextSetAsync(this.CancellationToken);
            this.ContinuationToken = response.Headers.Continuation;

            Assert.IsFalse(resultSetIterator.HasMoreResults );
            Assert.IsNull(response.Headers.Continuation);
        }

        [TestMethod]
        public void ValidateFillCosmosQueryRequestOptions()
        {
            Mock<CosmosQueryRequestOptions> options = new Mock<CosmosQueryRequestOptions>() { CallBase = true };

            CosmosRequestMessage request = new CosmosRequestMessage {
                OperationType = Internal.OperationType.SqlQuery
            };

            options.Object.EnableCrossPartitionQuery = true;
            options.Object.EnableScanInQuery = true;
            options.Object.SessionToken = "SessionToken";
            options.Object.ConsistencyLevel = ConsistencyLevel.BoundedStaleness;
            options.Object.FillRequestOptions(request);

            Assert.AreEqual(bool.TrueString, request.Headers[Internal.HttpConstants.HttpHeaders.IsQuery]);
            Assert.AreEqual(bool.TrueString, request.Headers[Internal.HttpConstants.HttpHeaders.EnableCrossPartitionQuery]);
            Assert.AreEqual(Internal.RuntimeConstants.MediaTypes.QueryJson, request.Headers[Internal.HttpConstants.HttpHeaders.ContentType]);
            Assert.AreEqual(bool.TrueString, request.Headers[Internal.HttpConstants.HttpHeaders.EnableScanInQuery]);
            Assert.AreEqual(options.Object.SessionToken, request.Headers[Internal.HttpConstants.HttpHeaders.SessionToken]);
            Assert.AreEqual(options.Object.ConsistencyLevel.ToString(), request.Headers[Internal.HttpConstants.HttpHeaders.ConsistencyLevel]);
            options.Verify(m => m.FillRequestOptions(It.Is<CosmosRequestMessage>(p => ReferenceEquals(p, request))), Times.Once);
        }

        [TestMethod]
        public async Task VerifyCosmosDefaultResultSetStreamIteratorOperationType()
        {
            CosmosClient mockClient = MockDocumentClient.CreateMockCosmosClient();
            
            CosmosDatabase database = new CosmosDatabase(mockClient, "database");
            CosmosContainer container = new CosmosContainer(database, "container");
            CosmosItems item = new CosmosItems(container);
            CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("select * from r");
            CosmosResultSetIterator setIterator = item
                .CreateItemQueryAsStream(sql, "pk", requestOptions: new CosmosQueryRequestOptions());

            TestHandler testHandler = new TestHandler((request, cancellationToken) => {
                Assert.AreEqual(
                    15, //OperationType.SqlQuery
                    (int)request.GetType().GetProperty("OperationType", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(request, null)
                );
                return TestHandler.ReturnSuccess();
            });
            mockClient.RequestHandler.InnerHandler = testHandler;
            mockClient.CosmosConfiguration.UseConnectionModeDirect();
            CosmosResponseMessage response = await setIterator.FetchNextSetAsync();

            testHandler = new TestHandler((request, cancellationToken) => {
                Assert.AreEqual(
                    14, //OperationType.Query
                    (int)request.GetType().GetProperty("OperationType", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(request, null)
                );
                return TestHandler.ReturnSuccess();
            });
            mockClient.RequestHandler.InnerHandler = testHandler;
            mockClient.CosmosConfiguration.UseConnectionModeGateway();
            response = await setIterator.FetchNextSetAsync();
        }

        private Task<CosmosResponseMessage> NextResultSetDelegate(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            // Validate that same contract is sent back on delegate
            Assert.IsTrue(object.ReferenceEquals(this.ContinuationToken, continuationToken));
            Assert.IsTrue(object.ReferenceEquals(this.Options, options));

            // CancellationToken is a struct and refs will not match
            Assert.AreEqual(this.CancellationToken.IsCancellationRequested, cancellationToken.IsCancellationRequested);

            return Task.FromResult(GetHttpResponse());
        }

        private CosmosResponseMessage GetHttpResponse()
        {
            CosmosResponseMessage response = new CosmosResponseMessage();
            if (this.ContinueNextExecution)
            {
                response.Headers.Add("x-ms-continuation", ResultSetIteratorTests.RandomString(10));
            }

            return response;
        }

        private static string RandomString(int length)
        {
            const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            Random random = new Random((int)DateTime.Now.Ticks);
            return new string(Enumerable.Repeat(Chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
