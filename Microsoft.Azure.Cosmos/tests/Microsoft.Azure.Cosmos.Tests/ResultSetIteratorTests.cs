//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class ResultSetIteratorTests
    {
        private int? MaxItemCount { get; set; }
        private string ContinuationToken { get; set; }
        private QueryRequestOptions Options { get; set; }
        private CancellationToken CancellationToken { get; set; }
        private bool ContinueNextExecution { get; set; }

        [TestMethod]
        public void ValidateFillQueryRequestOptions()
        {
            Mock<QueryRequestOptions> options = new Mock<QueryRequestOptions>() { CallBase = true };

            RequestMessage request = new RequestMessage
            {
                OperationType = OperationType.SqlQuery
            };

            options.Object.EnableScanInQuery = true;
            options.Object.SessionToken = "SessionToken";
            options.Object.ConsistencyLevel = (Cosmos.ConsistencyLevel)ConsistencyLevel.BoundedStaleness;
            options.Object.PopulateRequestOptions(request);

            Assert.AreEqual(bool.TrueString, request.Headers[HttpConstants.HttpHeaders.EnableScanInQuery]);
            Assert.AreEqual(options.Object.SessionToken, request.Headers[HttpConstants.HttpHeaders.SessionToken]);
            Assert.IsNull(request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel]);
            options.Verify(m => m.PopulateRequestOptions(It.Is<RequestMessage>(p => ReferenceEquals(p, request))), Times.Once);
        }

        [TestMethod]
        public async Task CosmosConflictsIteratorBuildsSettings()
        {
            string conflictResponsePayload = @"{ ""Conflicts"":[{
                 ""id"": ""Conflict1"",
                 ""operationType"": ""Replace"",
                 ""resourceType"": ""trigger""
                }]}";

            using CosmosClient mockClient = MockCosmosUtil.CreateMockCosmosClient(
                (cosmosClientBuilder) => cosmosClientBuilder.WithConnectionModeDirect());

            Container container = mockClient.GetContainer("database", "container");
            FeedIterator<ConflictProperties> feedIterator = container.Conflicts.GetConflictQueryIterator<ConflictProperties>();

            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                Assert.IsFalse(request.IsPartitionKeyRangeHandlerRequired);
                Assert.AreEqual(OperationType.ReadFeed, request.OperationType);
                Assert.AreEqual(ResourceType.Conflict, request.ResourceType);
                ResponseMessage handlerResponse = TestHandler.ReturnSuccess().Result;
                MemoryStream stream = new MemoryStream();
                StreamWriter writer = new StreamWriter(stream);
                writer.Write(conflictResponsePayload);
                writer.Flush();
                stream.Position = 0;

                handlerResponse.Content = stream;
                return Task.FromResult(handlerResponse);
            });

            mockClient.RequestHandler.InnerHandler = testHandler;
            FeedResponse<ConflictProperties> response = await feedIterator.ReadNextAsync();

            Assert.AreEqual(1, response.Count());

            ConflictProperties responseSettings = response.FirstOrDefault();
            Assert.IsNotNull(responseSettings);

            Assert.AreEqual("Conflict1", responseSettings.Id);
            Assert.AreEqual(Cosmos.OperationKind.Replace, responseSettings.OperationKind);
            Assert.AreEqual(typeof(TriggerProperties), responseSettings.ResourceType);
        }

        [TestMethod]
        public async Task CosmosConflictsStreamIteratorBuildsSettings()
        {
            string conflictResponsePayload = @"{ ""Conflicts"":[{
                 ""id"": ""Conflict1"",
                 ""operationType"": ""Replace"",
                 ""resourceType"": ""trigger""
                }]}";

            JObject jObject = JObject.Parse(conflictResponsePayload);
            using CosmosClient mockClient = MockCosmosUtil.CreateMockCosmosClient(
                (cosmosClientBuilder) => cosmosClientBuilder.WithConnectionModeDirect());

            Container container = mockClient.GetContainer("database", "container");
            FeedIterator feedIterator = container.Conflicts.GetConflictQueryStreamIterator();

            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                Assert.AreEqual(OperationType.ReadFeed, request.OperationType);
                Assert.AreEqual(ResourceType.Conflict, request.ResourceType);
                ResponseMessage handlerResponse = TestHandler.ReturnSuccess().Result;
                MemoryStream stream = new MemoryStream();
                StreamWriter writer = new StreamWriter(stream);
                writer.Write(conflictResponsePayload);
                writer.Flush();
                stream.Position = 0;

                handlerResponse.Content = stream;
                return Task.FromResult(handlerResponse);
            });

            mockClient.RequestHandler.InnerHandler = testHandler;
            ResponseMessage streamResponse = await feedIterator.ReadNextAsync();

            IEnumerable<ConflictProperties> response = CosmosFeedResponseSerializer.FromFeedResponseStream<ConflictProperties>(
                MockCosmosUtil.Serializer,
                streamResponse.Content);

            Assert.AreEqual(1, response.Count());

            ConflictProperties responseSettings = response.FirstOrDefault();
            Assert.IsNotNull(responseSettings);

            Assert.AreEqual("Conflict1", responseSettings.Id);
            Assert.AreEqual(Cosmos.OperationKind.Replace, responseSettings.OperationKind);
            Assert.AreEqual(typeof(TriggerProperties), responseSettings.ResourceType);
        }

        private Task<ResponseMessage> NextResultSetDelegate(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
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

        private ResponseMessage GetHttpResponse()
        {
            ResponseMessage response = new ResponseMessage();
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