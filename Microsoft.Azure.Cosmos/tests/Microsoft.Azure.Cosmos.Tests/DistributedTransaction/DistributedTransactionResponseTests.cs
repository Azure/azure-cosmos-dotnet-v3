// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using PartitionKey = Cosmos.PartitionKey;

    /// <summary>
    /// Unit tests for <see cref="DistributedTransactionResponse"/> covering null/malformed content,
    /// count-mismatch paths, MultiStatus promotion, idempotency-token resolution,
    /// IDisposable semantics, and the IsSuccessStatusCode boundaries.
    /// </summary>
    [TestClass]
    public class DistributedTransactionResponseTests
    {
        // Null or malformed content

        [TestMethod]
        [Description("When the response has no content body and the HTTP status is success, the SDK must return 500 because the server should always return a body on success.")]
        public async Task FromResponseMessage_NullContent_SuccessStatus_ReturnsInternalServerError()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK)
            {
                Content = null
            };

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                Guid.NewGuid(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.IsFalse(response.IsSuccessStatusCode);
        }

        [TestMethod]
        [Description("When the response has no content body and the HTTP status is an error, results should be padded with the error status code.")]
        public async Task FromResponseMessage_NullContent_ErrorStatus_PopulatesResultsWithErrorStatus()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 2);

            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.Conflict)
            {
                Content = null
            };

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                Guid.NewGuid(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.AreEqual(2, response.Count, "Count must equal the number of submitted operations.");

            for (int i = 0; i < response.Count; i++)
            {
                Assert.AreEqual(HttpStatusCode.Conflict, response[i].StatusCode,
                    $"Result[{i}] should carry the transaction-level error status.");
            }
        }

        [TestMethod]
        [Description("When the response body contains malformed JSON and the HTTP status is success, the SDK must return 500.")]
        public async Task FromResponseMessage_MalformedJson_SuccessStatus_ReturnsInternalServerError()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, "{invalid-json");

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                Guid.NewGuid(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.IsFalse(response.IsSuccessStatusCode);
        }

        [TestMethod]
        [Description("When the response body contains malformed JSON and the HTTP status is an error, results are padded with the error status code.")]
        public async Task FromResponseMessage_MalformedJson_ErrorStatus_PopulatesResultsWithErrorStatus()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 2);

            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.Conflict, "{invalid-json");

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                Guid.NewGuid(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
            Assert.AreEqual(2, response.Count);

            for (int i = 0; i < response.Count; i++)
            {
                Assert.AreEqual(HttpStatusCode.Conflict, response[i].StatusCode);
            }
        }

        // Count mismatch

        [TestMethod]
        [Description("When the server returns fewer results than submitted operations and the HTTP status is success, the SDK must return 500.")]
        public async Task FromResponseMessage_CountMismatch_FewerResults_SuccessStatus_Returns500()
        {
            // 2 operations submitted but server returns only 1 result
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 2);

            string json = @"{""operationResponses"":[{""index"":0,""statuscode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                Guid.NewGuid(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.IsFalse(response.IsSuccessStatusCode);
        }

        [TestMethod]
        [Description("When the server returns fewer results than submitted operations and the HTTP status is an error, results are padded.")]
        public async Task FromResponseMessage_CountMismatch_FewerResults_ErrorStatus_PadsResults()
        {
            // 3 operations submitted but server returns only 1 result
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 3);

            string json = @"{""operationResponses"":[{""index"":0,""statuscode"":409}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.Conflict, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                Guid.NewGuid(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
            Assert.AreEqual(3, response.Count, "Count must equal the number of submitted operations.");

            for (int i = 0; i < response.Count; i++)
            {
                Assert.AreEqual(HttpStatusCode.Conflict, response[i].StatusCode);
            }
        }

        // MultiStatus promotion

        [TestMethod]
        [Description("A 207 MultiStatus response promotes the status code of the first failing operation to the overall response status.")]
        public async Task FromResponseMessage_MultiStatus_PromotesFirstNonDependencyFailure()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 2);

            // index 0 succeeds, index 1 fails with 409
            string json = @"{""operationResponses"":[{""index"":0,""statuscode"":201},{""index"":1,""statuscode"":409}]}";
            ResponseMessage responseMessage = BuildResponseMessage((HttpStatusCode)StatusCodes.MultiStatus, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                Guid.NewGuid(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode,
                "The overall status should be promoted from 207 to the first failing operation status (409).");
            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.AreEqual(2, response.Count);
        }

        [TestMethod]
        [Description("A 207 MultiStatus response scans past leading successes to find and promote the first failing operation status.")]
        public async Task FromResponseMessage_MultiStatus_SkipsSuccessesBeforeFirstFailure()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 3);

            // index 0 and 1 succeed; index 2 fails with 503
            string json = @"{""operationResponses"":[{""index"":0,""statuscode"":201},{""index"":1,""statuscode"":200},{""index"":2,""statuscode"":503}]}";
            ResponseMessage responseMessage = BuildResponseMessage((HttpStatusCode)StatusCodes.MultiStatus, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                Guid.NewGuid(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode,
                "Promotion must scan past successes (201, 200) and find the first error (503).");
            Assert.IsFalse(response.IsSuccessStatusCode);
        }

        [TestMethod]
        [Description("When all operations in a 207 response report FailedDependency (424), no promotion occurs and the overall status remains 207.")]
        public async Task FromResponseMessage_MultiStatus_AllFailedDependency_StatusRemainsMultiStatus()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 2);

            // Both results are FailedDependency (424) — excluded from promotion logic
            string json = $@"{{""operationResponses"":[{{""index"":0,""statuscode"":{(int)StatusCodes.FailedDependency}}},{{""index"":1,""statuscode"":{(int)StatusCodes.FailedDependency}}}]}}";
            ResponseMessage responseMessage = BuildResponseMessage((HttpStatusCode)StatusCodes.MultiStatus, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                Guid.NewGuid(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual((HttpStatusCode)StatusCodes.MultiStatus, response.StatusCode,
                "Status must remain 207 when all operation results are FailedDependency (excluded from promotion).");
        }

        // Idempotency token resolution

        [TestMethod]
        [Description("When the IdempotencyToken header is absent from the response, the request token is used as the fallback.")]
        public async Task FromResponseMessage_IdempotencyToken_MissingFromHeader_FallsBackToRequestToken()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            Guid requestToken = Guid.NewGuid();

            string json = @"{""operationResponses"":[{""index"":0,""statuscode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);
            // No IdempotencyToken header added

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                requestToken,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(requestToken, response.IdempotencyToken,
                "The request token must be used when the response header is absent.");
        }

        [TestMethod]
        [Description("When the IdempotencyToken response header contains a non-GUID value, the SDK falls back to the request token.")]
        public async Task FromResponseMessage_IdempotencyToken_InvalidGuidInHeader_FallsBackToRequestToken()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            Guid requestToken = Guid.NewGuid();

            string json = @"{""operationResponses"":[{""index"":0,""statuscode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);
            responseMessage.Headers.Add(HttpConstants.HttpHeaders.IdempotencyToken, "not-a-valid-guid");

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                requestToken,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(requestToken, response.IdempotencyToken,
                "An unparseable header value must fall back to the request token.");
        }

        // IDisposable and ObjectDisposed

        [TestMethod]
        [Description("Dispose() must set result ResourceStreams to null so callers cannot accidentally use a closed stream.")]
        public async Task Dispose_ReleasesResultResourceStreams()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            string json = @"{""operationResponses"":[{""index"":0,""statuscode"":201,""resourcebody"":{""id"":""item1""}}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                Guid.NewGuid(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsNotNull(response[0].ResourceStream, "ResourceStream should be populated from resourcebody before Dispose.");

            response.Dispose();

            // After dispose, result list is nulled — indexer throws ObjectDisposedException
            Assert.ThrowsException<ObjectDisposedException>(() => _ = response[0]);
        }

        [TestMethod]
        [Description("Calling Dispose() a second time must be a safe no-op.")]
        public async Task Dispose_SecondCall_DoesNotThrow()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statuscode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                Guid.NewGuid(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            response.Dispose();
            response.Dispose(); // must not throw
        }

        [TestMethod]
        [Description("Accessing a result by index after Dispose() must throw ObjectDisposedException.")]
        public async Task Indexer_AfterDispose_ThrowsObjectDisposedException()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statuscode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                Guid.NewGuid(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            response.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() => _ = response[0]);
        }

        // IsSuccessStatusCode boundaries

        [TestMethod]
        [Description("HTTP 200 is a success status code.")]
        public async Task IsSuccessStatusCode_200_ReturnsTrue()
        {
            DistributedTransactionResponse response = await BuildResponseWithStatusAsync(HttpStatusCode.OK, operationCount: 1);
            Assert.IsTrue(response.IsSuccessStatusCode);
        }

        [TestMethod]
        [Description("HTTP 299 is the last success status code.")]
        public async Task IsSuccessStatusCode_299_ReturnsTrue()
        {
            DistributedTransactionResponse response = await BuildResponseWithStatusAsync((HttpStatusCode)299, operationCount: 1);
            Assert.IsTrue(response.IsSuccessStatusCode);
        }

        [TestMethod]
        [Description("HTTP 300 is not a success status code.")]
        public async Task IsSuccessStatusCode_300_ReturnsFalse()
        {
            DistributedTransactionResponse response = await BuildResponseWithStatusAsync((HttpStatusCode)300, operationCount: 1, isError: true);
            Assert.IsFalse(response.IsSuccessStatusCode);
        }

        [TestMethod]
        [Description("HTTP 199 is not a success status code.")]
        public async Task IsSuccessStatusCode_199_ReturnsFalse()
        {
            DistributedTransactionResponse response = await BuildResponseWithStatusAsync((HttpStatusCode)199, operationCount: 1, isError: true);
            Assert.IsFalse(response.IsSuccessStatusCode);
        }

        // Indexer and enumerator

        [TestMethod]
        [Description("Accessing a valid index returns the expected result with the correct StatusCode and Index.")]
        public async Task Indexer_ValidIndex_ReturnsExpectedResult()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 2);
            string json = @"{""operationResponses"":[{""index"":0,""statuscode"":201},{""index"":1,""statuscode"":200}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                Guid.NewGuid(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.Created, response[0].StatusCode);
            Assert.AreEqual(0, response[0].Index);
            Assert.AreEqual(HttpStatusCode.OK, response[1].StatusCode);
            Assert.AreEqual(1, response[1].Index);
        }

        [TestMethod]
        [Description("Accessing a negative index must throw ArgumentOutOfRangeException.")]
        public async Task Indexer_NegativeIndex_ThrowsArgumentOutOfRangeException()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statuscode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                Guid.NewGuid(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = response[-1]);
        }

        [TestMethod]
        [Description("Accessing index equal to Count must throw ArgumentOutOfRangeException.")]
        public async Task Indexer_IndexEqualsCount_ThrowsArgumentOutOfRangeException()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statuscode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                Guid.NewGuid(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = response[response.Count]);
        }

        [TestMethod]
        [Description("GetEnumerator yields all results in the same order as index access.")]
        public async Task GetEnumerator_ReturnsAllResults_InOrder()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 3);
            string json = @"{""operationResponses"":[{""index"":0,""statuscode"":201},{""index"":1,""statuscode"":200},{""index"":2,""statuscode"":204}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                Guid.NewGuid(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            List<DistributedTransactionOperationResult> enumerated = new List<DistributedTransactionOperationResult>();
            foreach (DistributedTransactionOperationResult result in response)
            {
                enumerated.Add(result);
            }

            Assert.AreEqual(3, enumerated.Count);
            for (int i = 0; i < response.Count; i++)
            {
                Assert.AreEqual(response[i].StatusCode, enumerated[i].StatusCode,
                    $"Enumerator result[{i}] must match indexer result[{i}].");
                Assert.AreEqual(response[i].Index, enumerated[i].Index);
            }
        }

        [TestMethod]
        [Description("Count matches the number of parsed operation results in the response JSON.")]
        public async Task Count_ReturnsNumberOfParsedResults()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 4);
            string json = @"{""operationResponses"":[{""index"":0,""statuscode"":201},{""index"":1,""statuscode"":201},{""index"":2,""statuscode"":201},{""index"":3,""statuscode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                Guid.NewGuid(),
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(4, response.Count);
        }

        // Helpers

        /// <summary>
        /// Builds a <see cref="DistributedTransactionServerRequest"/> with <paramref name="operationCount"/>
        /// simple Create operations (no resource body — safe for response-parsing tests).
        /// </summary>
        private static async Task<DistributedTransactionServerRequest> BuildServerRequestAsync(int operationCount)
        {
            List<DistributedTransactionOperation> operations = new List<DistributedTransactionOperation>();
            for (int i = 0; i < operationCount; i++)
            {
                operations.Add(new DistributedTransactionOperation(
                    operationType: OperationType.Create,
                    operationIndex: i,
                    database: "testDb",
                    container: "testContainer",
                    partitionKey: new PartitionKey($"pk{i}")));
            }

            return await DistributedTransactionServerRequest.CreateAsync(
                operations,
                MockCosmosUtil.Serializer,
                CancellationToken.None);
        }

        /// <summary>
        /// Builds a <see cref="ResponseMessage"/> with the given status code and JSON body.
        /// </summary>
        private static ResponseMessage BuildResponseMessage(HttpStatusCode statusCode, string json)
        {
            return new ResponseMessage(statusCode)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(json))
            };
        }

        /// <summary>
        /// Builds a <see cref="DistributedTransactionResponse"/> driven by the given status code.
        /// For success codes the JSON must have matching result count; for error codes the results are padded.
        /// </summary>
        private static async Task<DistributedTransactionResponse> BuildResponseWithStatusAsync(
            HttpStatusCode statusCode,
            int operationCount,
            bool isError = false)
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount);

            string json;
            if (isError)
            {
                // Produce intentionally mismatched JSON so the padded-results path is exercised
                json = $@"{{""operationResponses"":[{{""index"":0,""statuscode"":{(int)statusCode}}}]}}";
            }
            else
            {
                List<string> results = new List<string>();
                for (int i = 0; i < operationCount; i++)
                {
                    results.Add($@"{{""index"":{i},""statuscode"":{(int)statusCode}}}");
                }
                json = $@"{{""operationResponses"":[{string.Join(",", results)}]}}";
            }

            return await DistributedTransactionResponse.FromResponseMessageAsync(
                BuildResponseMessage(statusCode, json),
                serverRequest,
                MockCosmosUtil.Serializer,
                Guid.NewGuid(),
                NoOpTrace.Singleton,
                CancellationToken.None);
        }
    }
}
