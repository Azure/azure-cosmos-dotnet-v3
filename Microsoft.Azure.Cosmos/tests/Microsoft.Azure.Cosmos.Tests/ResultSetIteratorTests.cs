//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

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

            CosmosFeedIterator resultSetIterator = new CosmosResultSetIteratorCore(
                this.MaxItemCount,
                this.ContinuationToken,
                this.Options,
                this.NextResultSetDelegate);

            Assert.IsTrue(resultSetIterator.HasMoreResults);

            CosmosResponseMessage response = await resultSetIterator.FetchNextSetAsync(this.CancellationToken);
            this.ContinuationToken = response.Headers.Continuation;

            Assert.IsTrue(resultSetIterator.HasMoreResults);
            this.ContinueNextExecution = false;

            response = await resultSetIterator.FetchNextSetAsync(this.CancellationToken);
            this.ContinuationToken = response.Headers.Continuation;

            Assert.IsFalse(resultSetIterator.HasMoreResults);
            Assert.IsNull(response.Headers.Continuation);
        }

        [TestMethod]
        public void ValidateFillCosmosQueryRequestOptions()
        {
            Mock<CosmosQueryRequestOptions> options = new Mock<CosmosQueryRequestOptions>() { CallBase = true };

            CosmosRequestMessage request = new CosmosRequestMessage
            {
                OperationType = OperationType.SqlQuery
            };

            options.Object.EnableCrossPartitionQuery = true;
            options.Object.EnableScanInQuery = true;
            options.Object.SessionToken = "SessionToken";
            options.Object.ConsistencyLevel = (Cosmos.ConsistencyLevel)ConsistencyLevel.BoundedStaleness;
            options.Object.FillRequestOptions(request);

            Assert.AreEqual(bool.TrueString, request.Headers[HttpConstants.HttpHeaders.IsQuery]);
            Assert.AreEqual(bool.TrueString, request.Headers[HttpConstants.HttpHeaders.EnableCrossPartitionQuery]);
            Assert.AreEqual(RuntimeConstants.MediaTypes.QueryJson, request.Headers[HttpConstants.HttpHeaders.ContentType]);
            Assert.AreEqual(bool.TrueString, request.Headers[HttpConstants.HttpHeaders.EnableScanInQuery]);
            Assert.AreEqual(options.Object.SessionToken, request.Headers[HttpConstants.HttpHeaders.SessionToken]);
            Assert.AreEqual(options.Object.ConsistencyLevel.ToString(), request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel]);
            options.Verify(m => m.FillRequestOptions(It.Is<CosmosRequestMessage>(p => ReferenceEquals(p, request))), Times.Once);
        }

        // DEVNOTE: Query is not wired into the handler pipeline yet.
        [Ignore]
        [TestMethod]
        public async Task VerifyCosmosDefaultResultSetStreamIteratorOperationType()
        {
            CosmosClient mockClient = MockCosmosUtil.CreateMockCosmosClient(
                (cosmosClientBuilder) => cosmosClientBuilder.UseConnectionModeDirect());

            CosmosContainer container = mockClient.Databases["database"].Containers["container"];
            CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("select * from r");
            CosmosFeedIterator setIterator = container.Items.CreateItemQueryAsStream(
                sqlQueryDefinition: sql,
                maxConcurrency: 1,
                partitionKey: "pk",
                requestOptions: new CosmosQueryRequestOptions());

            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                Assert.AreEqual(
                    15, //OperationType.SqlQuery
                    (int)request.GetType().GetProperty("OperationType", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(request, null)
                );
                return TestHandler.ReturnSuccess();
            });

            mockClient.RequestHandler.InnerHandler = testHandler;
            CosmosResponseMessage response = await setIterator.FetchNextSetAsync();

            //Test gateway mode
            mockClient = MockCosmosUtil.CreateMockCosmosClient(
                (cosmosClientBuilder) => cosmosClientBuilder.UseConnectionModeGateway());
            container = mockClient.Databases["database"].Containers["container"];
            setIterator = container.Items.CreateItemQueryAsStream(
                sqlQueryDefinition: sql,
                maxConcurrency: 1,
                partitionKey: "pk",
                requestOptions: new CosmosQueryRequestOptions());
            testHandler = new TestHandler((request, cancellationToken) =>
            {
                Assert.AreEqual(
                    14, //OperationType.Query
                    (int)request.GetType().GetProperty("OperationType", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(request, null)
                );
                return TestHandler.ReturnSuccess();
            });

            mockClient.RequestHandler.InnerHandler = testHandler;
            response = await setIterator.FetchNextSetAsync();
        }

        [TestMethod]
        public async Task CosmosConflictsIteratorBuildsSettings()
        {
            string conflictResponsePayload = @"{ 'Data':[{
                 id: 'Conflict1',
                 operationType: 'Replace',
                 resourceType: 'trigger'
                }]}";

            CosmosClient mockClient = MockCosmosUtil.CreateMockCosmosClient(
                (cosmosClientBuilder) => cosmosClientBuilder.UseConnectionModeDirect());

            CosmosContainer container = mockClient.Databases["database"].Containers["container"];
            CosmosFeedIterator<CosmosConflict> setIterator = container.GetConflictsIterator();

            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                Assert.AreEqual(OperationType.ReadFeed, request.OperationType);
                Assert.AreEqual(ResourceType.Conflict, request.ResourceType);
                CosmosResponseMessage handlerResponse = TestHandler.ReturnSuccess().Result;
                MemoryStream stream = new MemoryStream();
                StreamWriter writer = new StreamWriter(stream);
                writer.Write(conflictResponsePayload);
                writer.Flush();
                stream.Position = 0;

                handlerResponse.Content = stream;
                return Task.FromResult(handlerResponse);
            });

            mockClient.RequestHandler.InnerHandler = testHandler;
            CosmosFeedResponse<CosmosConflict> response = await setIterator.FetchNextSetAsync();

            Assert.AreEqual(1, response.Count());

            CosmosConflict responseSettings = response.FirstOrDefault();
            Assert.IsNotNull(responseSettings);

            Assert.AreEqual("Conflict1", responseSettings.Id);
            Assert.AreEqual(Cosmos.OperationKind.Replace, responseSettings.OperationKind);
            Assert.AreEqual(typeof(CosmosTrigger), responseSettings.ResourceType);
        }

        [TestMethod]
        public async Task CosmosConflictsStreamIteratorBuildsSettings()
        {
            string conflictResponsePayload = @"{ 'Data':[{
                 id: 'Conflict1',
                 operationType: 'Replace',
                 resourceType: 'trigger'
                }]}";

            CosmosClient mockClient = MockCosmosUtil.CreateMockCosmosClient(
                (cosmosClientBuilder) => cosmosClientBuilder.UseConnectionModeDirect());

            CosmosContainer container = mockClient.Databases["database"].Containers["container"];
            CosmosFeedIterator setIterator = container.GetConflictsStreamIterator();

            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                Assert.AreEqual(OperationType.ReadFeed, request.OperationType);
                Assert.AreEqual(ResourceType.Conflict, request.ResourceType);
                CosmosResponseMessage handlerResponse = TestHandler.ReturnSuccess().Result;
                MemoryStream stream = new MemoryStream();
                StreamWriter writer = new StreamWriter(stream);
                writer.Write(conflictResponsePayload);
                writer.Flush();
                stream.Position = 0;

                handlerResponse.Content = stream;
                return Task.FromResult(handlerResponse);
            });

            mockClient.RequestHandler.InnerHandler = testHandler;
            CosmosResponseMessage streamResponse = await setIterator.FetchNextSetAsync();

            Collection<CosmosConflict> response = new CosmosDefaultJsonSerializer().FromStream<CosmosFeedResponseUtil<CosmosConflict>>(streamResponse.Content).Data;

            Assert.AreEqual(1, response.Count());

            CosmosConflict responseSettings = response.FirstOrDefault();
            Assert.IsNotNull(responseSettings);

            Assert.AreEqual("Conflict1", responseSettings.Id);
            Assert.AreEqual(Cosmos.OperationKind.Replace, responseSettings.OperationKind);
            Assert.AreEqual(typeof(CosmosTrigger), responseSettings.ResourceType);
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

            return Task.FromResult(this.GetHttpResponse());
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
