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
        [Description("A 412 PreconditionFailed response with one matching result must return PreconditionFailed status, IsSuccessStatusCode false, Count 1, and result[0].StatusCode PreconditionFailed.")]
        public async Task FromResponseMessage_PreconditionFailed_ReturnsFailureStatus()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            string json = $@"{{""operationResponses"":[{{""index"":0,""statusCode"":{(int)HttpStatusCode.PreconditionFailed}}}]}}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.PreconditionFailed, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.PreconditionFailed, response.StatusCode);
            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.AreEqual(1, response.Count);
            Assert.AreEqual(HttpStatusCode.PreconditionFailed, response[0].StatusCode);
        }

        [TestMethod]
        [Description("When the response body contains malformed JSON and the HTTP status is success, the SDK must return 500 with a deserialization-failure error message.")]
        public async Task FromResponseMessage_MalformedJson_SuccessStatus_ReturnsInternalServerError()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, "{invalid-json");

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.AreEqual(ClientResources.ServerResponseDeserializationFailure, response.ErrorMessage);
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
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
            Assert.AreEqual(2, response.Count);

            for (int i = 0; i < response.Count; i++)
            {
                Assert.AreEqual(HttpStatusCode.Conflict, response[i].StatusCode);
            }
        }

        [DataTestMethod]
        [Description("When per-operation fields have wrong types or non-object entries, parsing fails and a success response is converted to InternalServerError with a deserialization-failure message.")]
        [DataRow(@"{""operationResponses"":[{""index"":0,""statusCode"":""abc""}]}", DisplayName = "statusCode wrong type")]
        [DataRow(@"{""operationResponses"":[{""index"":0,""statusCode"":449,""subStatusCode"":""abc""}]}", DisplayName = "subStatusCode wrong type")]
        [DataRow(@"{""operationResponses"":[{""index"":0,""statusCode"":201,""requestCharge"":""abc""}]}", DisplayName = "requestCharge wrong type")]
        [DataRow(@"{""operationResponses"":[{""index"":""abc"",""statusCode"":201}]}", DisplayName = "index wrong type")]
        [DataRow(@"{""operationResponses"":[null]}", DisplayName = "non-object element (null)")]
        public async Task FromResponseMessage_OperationResult_InvalidElement_SuccessStatus_ReturnsInternalServerError(string json)
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.AreEqual(ClientResources.ServerResponseDeserializationFailure, response.ErrorMessage);
        }

        [TestMethod]
        [Description("Top-level lookups are case-insensitive; PascalCase 'OperationResponses' is accepted.")]
        public async Task FromResponseMessage_OperationResponses_PascalCaseKey_SuccessStatus_ParsesSuccessfully()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""OperationResponses"":[{""index"":0,""statusCode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual(1, response.Count);
            Assert.AreEqual(HttpStatusCode.Created, response[0].StatusCode);
        }

        // Count mismatch

        [TestMethod]
        [Description("When the server returns fewer results than submitted operations and the HTTP status is success, the SDK must return 500.")]
        public async Task FromResponseMessage_CountMismatch_FewerResults_SuccessStatus_Returns500()
        {
            // 2 operations submitted but server returns only 1 result
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 2);

            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.IsFalse(response.IsSuccessStatusCode);
        }

        [TestMethod]
        [Description("When a count-mismatch synthetic 500 is built, the sub-status code from the wire header must be preserved rather than discarded as Unknown.")]
        public async Task FromResponseMessage_CountMismatch_SuccessStatus_PreservesWireSubStatusCode()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 2);

            // 2 operations submitted but server returns only 1 result — triggers the synthetic 500 path.
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);
            responseMessage.Headers.SubStatusCode = (SubStatusCodes)1009; // a non-Unknown sub-status code

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.AreEqual((SubStatusCodes)1009, response.SubStatusCode,
                "The wire sub-status code must be preserved in the synthetic 500 response rather than replaced with Unknown.");
        }

        [TestMethod]
        [Description("When the server returns fewer results than submitted operations and the HTTP status is an error, results are padded.")]
        public async Task FromResponseMessage_CountMismatch_FewerResults_ErrorStatus_PadsResults()
        {
            // 3 operations submitted but server returns only 1 result
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 3);

            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":409}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.Conflict, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
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
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201},{""index"":1,""statusCode"":409}]}";
            ResponseMessage responseMessage = BuildResponseMessage((HttpStatusCode)StatusCodes.MultiStatus, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
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
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201},{""index"":1,""statusCode"":200},{""index"":2,""statusCode"":503}]}";
            ResponseMessage responseMessage = BuildResponseMessage((HttpStatusCode)StatusCodes.MultiStatus, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
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
            string json = $@"{{""operationResponses"":[{{""index"":0,""statusCode"":{(int)StatusCodes.FailedDependency}}},{{""index"":1,""statusCode"":{(int)StatusCodes.FailedDependency}}}]}}";
            ResponseMessage responseMessage = BuildResponseMessage((HttpStatusCode)StatusCodes.MultiStatus, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual((HttpStatusCode)StatusCodes.MultiStatus, response.StatusCode,
                "Status must remain 207 when all operation results are FailedDependency (excluded from promotion).");
        }

        [TestMethod]
        [Description("A 207 MultiStatus response with a leading FailedDependency (424) skips the 424 marker and promotes the next genuine failure (409).")]
        public async Task FromResponseMessage_MultiStatus_SkipsFailedDependency_PromotesNextFailure()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 2);

            // index 0 is a FailedDependency (424) cascade marker — excluded; index 1 fails with 409
            string json = $@"{{""operationResponses"":[{{""index"":0,""statusCode"":{(int)StatusCodes.FailedDependency}}},{{""index"":1,""statusCode"":409}}]}}";
            ResponseMessage responseMessage = BuildResponseMessage((HttpStatusCode)StatusCodes.MultiStatus, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode,
                "Promotion must skip the leading FailedDependency (424) marker and promote the next genuine failure (409).");
            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.AreEqual(2, response.Count);
        }

        [TestMethod]
        [Description("A 207 MultiStatus response with a per-op RetryWith (449) followed by NotFound (404) promotes the overall status to 449 (449 is not excluded from promotion).")]
        public async Task FromResponseMessage_MultiStatus_PromotesRetryWith449()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 2);

            // index 0 fails with 449 RetryWith; index 1 fails with 404 NotFound
            string json = $@"{{""operationResponses"":[{{""index"":0,""statusCode"":{(int)StatusCodes.RetryWith}}},{{""index"":1,""statusCode"":404}}]}}";
            ResponseMessage responseMessage = BuildResponseMessage((HttpStatusCode)StatusCodes.MultiStatus, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual((HttpStatusCode)StatusCodes.RetryWith, response.StatusCode,
                "The first failing operation status (449 RetryWith) must be promoted to the overall response status.");
            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.AreEqual(2, response.Count);
        }

        // Idempotency token resolution

        [DataTestMethod]
        [Description("When the IdempotencyToken response header is absent or unparseable, the SDK falls back to the request token.")]
        [DataRow(null, DisplayName = "Header absent")]
        [DataRow("not-a-valid-guid", DisplayName = "Invalid GUID in header")]
        public async Task FromResponseMessage_IdempotencyToken_FallsBackToRequestToken(string headerValue)
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            if (headerValue != null)
            {
                responseMessage.Headers.Add(HttpConstants.HttpHeaders.IdempotencyToken, headerValue);
            }

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(serverRequest.IdempotencyToken, response.IdempotencyToken,
                "The request token must be used when the response header is absent or unparseable.");
        }

        // IDisposable and ObjectDisposed

        [TestMethod]
        [Description("Calling Dispose() a second time must be a safe no-op.")]
        public async Task Dispose_SecondCall_DoesNotThrow()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
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
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            response.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() => _ = response[0]);
        }

        // IsSuccessStatusCode boundaries

        [DataTestMethod]
        [Description("HTTP success-status boundaries: [200, 299] are success, anything outside is not.")]
        [DataRow(200, true, DisplayName = "200 is success")]
        [DataRow(299, true, DisplayName = "299 is success boundary")]
        [DataRow(300, false, DisplayName = "300 is not success")]
        [DataRow(199, false, DisplayName = "199 is not success")]
        public async Task IsSuccessStatusCode_Boundaries(int statusCode, bool expected)
        {
            bool isError = !expected;
            DistributedTransactionResponse response = await BuildResponseWithStatusAsync((HttpStatusCode)statusCode, operationCount: 1, isError: isError);
            Assert.AreEqual(expected, response.IsSuccessStatusCode);
        }

        // Indexer and enumerator

        [TestMethod]
        [Description("Accessing a valid index returns the expected result with the correct StatusCode and Index.")]
        public async Task Indexer_ValidIndex_ReturnsExpectedResult()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 2);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201},{""index"":1,""statusCode"":200}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.Created, response[0].StatusCode);
            Assert.AreEqual(0, response[0].Index);
            Assert.AreEqual(HttpStatusCode.OK, response[1].StatusCode);
            Assert.AreEqual(1, response[1].Index);
        }

        [DataTestMethod]
        [Description("Accessing an out-of-range index must throw ArgumentOutOfRangeException.")]
        [DataRow(-1, DisplayName = "Negative index")]
        [DataRow(1, DisplayName = "Index equals count")]
        public async Task Indexer_OutOfRange_ThrowsArgumentOutOfRangeException(int index)
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _ = response[index]);
        }

        [TestMethod]
        [Description("GetEnumerator yields all results in the same order as index access.")]
        public async Task GetEnumerator_ReturnsAllResults_InOrder()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 3);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201},{""index"":1,""statusCode"":200},{""index"":2,""statusCode"":204}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
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
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201},{""index"":1,""statusCode"":201},{""index"":2,""statusCode"":201},{""index"":3,""statusCode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(4, response.Count);
        }

        // Operation result property deserialization

        [TestMethod]
        [Description("SubStatusCode deserializes from the 'subStatusCode' JSON property as a SubStatusCodes enum value.")]
        public async Task FromResponseMessage_OperationResult_SubStatusCode_DeserializesCorrectly()
        {
            const uint expectedSubStatusCode = 449; // RetryWith
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            string json = $@"{{""operationResponses"":[{{""index"":0,""statusCode"":449,""subStatusCode"":{expectedSubStatusCode}}}]}}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual((SubStatusCodes)expectedSubStatusCode, response[0].SubStatusCode,
                "SubStatusCode must equal the enum cast of the uint value from the JSON 'subStatusCode' field.");
        }

        [TestMethod]
        [Description("RequestCharge deserializes from the 'requestCharge' JSON property.")]
        public async Task FromResponseMessage_OperationResult_RequestCharge_DeserializesCorrectly()
        {
            const double expectedRequestCharge = 5.43;
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            string json = $@"{{""operationResponses"":[{{""index"":0,""statusCode"":201,""requestCharge"":{expectedRequestCharge}}}]}}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(expectedRequestCharge, response[0].RequestCharge,
                "RequestCharge must equal the value from the JSON 'requestCharge' field.");
        }

        [TestMethod]
        [Description("ETag deserializes from the 'etag' JSON property.")]
        public async Task FromResponseMessage_OperationResult_ETag_DeserializesCorrectly()
        {
            const string expectedETag = "etag-value-abc123";
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            string json = $@"{{""operationResponses"":[{{""index"":0,""statusCode"":201,""etag"":""{expectedETag}""}}]}}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(expectedETag, response[0].ETag,
                "ETag must equal the value from the JSON 'etag' field.");
        }

        [TestMethod]
        [Description("resourceBody lookup is case-insensitive; PascalCase 'ResourceBody' populates ResourceStream.")]
        public async Task FromResponseMessage_OperationResult_ResourceBody_PascalCaseKey_Deserializes()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201,""ResourceBody"":{""id"":""item1""}}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsNotNull(response[0].ResourceStream, "ResourceStream should be populated because property name lookup is case-insensitive.");
        }

        [TestMethod]
        [Description("SessionToken is assembled as {pkRangeId}:{lsn} from the separate 'sessionToken' (LSN-only) and 'partitionKeyRangeId' JSON fields.")]
        public async Task FromResponseMessage_OperationResult_SessionToken_DeserializesCorrectly()
        {
            const string lsnOnly = "12345";
            const string pkRangeId = "0";
            const string expectedSessionToken = "0:12345";
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            string json = $@"{{""operationResponses"":[{{""index"":0,""statusCode"":201,""sessionToken"":""{lsnOnly}"",""partitionKeyRangeId"":""{pkRangeId}""}}]}}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(expectedSessionToken, response[0].SessionToken,
                "SessionToken must be assembled as {pkRangeId}:{lsn} from the two separate JSON fields.");
        }

        [TestMethod]
        [Description("When partitionKeyRangeId is absent, FromJson sets SessionToken to null so MergeSessionTokens skips the operation.")]
        // TODO(issue#5857): Remove once the coordinator starts emitting partitionKeyRangeId for all operations.
        public async Task FromResponseMessage_OperationResult_SessionToken_NullWhenPartitionKeyRangeIdAbsent()
        {
            const string lsnOnly = "12345";
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            // partitionKeyRangeId field is omitted — current server behavior
            string json = $@"{{""operationResponses"":[{{""index"":0,""statusCode"":201,""sessionToken"":""{lsnOnly}""}}]}}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsNull(response[0].SessionToken,
                "SessionToken must be null when partitionKeyRangeId is absent so the merge is skipped.");
        }

        [DataTestMethod]
        [DataRow("", DisplayName = "Empty string partitionKeyRangeId")]
        [DataRow(" ", DisplayName = "Whitespace-only partitionKeyRangeId")]
        [DataRow("   ", DisplayName = "Multiple spaces partitionKeyRangeId")]
        [Description("When partitionKeyRangeId is present but empty or whitespace, FromJson sets SessionToken to null " +
                     "so MergeSessionTokens skips the operation. The server has no validation on this field and can " +
                     "send blank values; failing the commit would be worse than skipping the merge.")]
        public async Task FromResponseMessage_OperationResult_SessionToken_NullWhenPartitionKeyRangeIdIsBlank(string pkRangeId)
        {
            const string lsnOnly = "12345";
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            string json = $@"{{""operationResponses"":[{{""index"":0,""statusCode"":201,""sessionToken"":""{lsnOnly}"",""partitionKeyRangeId"":""{pkRangeId}""}}]}}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsNull(response[0].SessionToken,
                $"SessionToken must be null when partitionKeyRangeId is '{pkRangeId}' (empty/whitespace) so the merge is safely skipped.");
        }

        [DataTestMethod]
        [DataRow(" ", DisplayName = "Single space sessionToken")]
        [DataRow("   ", DisplayName = "Multiple spaces sessionToken")]
        [Description("When sessionToken is whitespace-only, FromJson treats it the same as absent — SessionToken remains null.")]
        public async Task FromResponseMessage_OperationResult_SessionToken_NullWhenSessionTokenIsWhitespace(string whitespaceToken)
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            string json = $@"{{""operationResponses"":[{{""index"":0,""statusCode"":201,""sessionToken"":""{whitespaceToken}"",""partitionKeyRangeId"":""0""}}]}}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsNull(response[0].SessionToken,
                $"SessionToken must be null when the sessionToken value is whitespace ('{whitespaceToken}').");
        }

        [DataTestMethod]
        [DataRow("0:-1#425344#1=12345", "0:-1#425344#1=12345", DisplayName = "Well-formed canonical token preserved")]
        [DataRow("3:500", "3:500", DisplayName = "Simple pkRangeId:lsn preserved")]
        [Description("When sessionToken is already in canonical {pkRangeId}:{lsn} form (colon at position > 0 with content on both sides), FromJson leaves it as-is even without partitionKeyRangeId.")]
        public async Task FromResponseMessage_OperationResult_SessionToken_PreservedWhenAlreadyCanonical(string token, string expected)
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            // sessionToken is already canonical; partitionKeyRangeId is absent
            string json = $@"{{""operationResponses"":[{{""index"":0,""statusCode"":201,""sessionToken"":""{token}""}}]}}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(expected, response[0].SessionToken,
                $"A well-formed canonical token '{token}' must be left as-is.");
        }

        [DataTestMethod]
        [DataRow(":-1#425344", "3", DisplayName = "Leading colon (no pkRangeId) — not canonical, assembles with pkRangeId")]
        [DataRow("3:", "5", DisplayName = "Trailing colon only (no LSN) — not canonical, assembles with pkRangeId")]
        [Description("Session tokens with a colon at position 0 or at the last character are not valid canonical tokens — they get assembled with the provided partitionKeyRangeId.")]
        public async Task FromResponseMessage_OperationResult_SessionToken_AssembledWhenColonIsAtEdge(string token, string pkRangeId)
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            string json = $@"{{""operationResponses"":[{{""index"":0,""statusCode"":201,""sessionToken"":""{token}"",""partitionKeyRangeId"":""{pkRangeId}""}}]}}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(pkRangeId + ":" + token, response[0].SessionToken,
                $"A token with edge colon '{token}' must be assembled with pkRangeId '{pkRangeId}'.");
        }

        [TestMethod]
        [Description("When sessionToken is absent entirely, SessionToken remains null regardless of partitionKeyRangeId.")]
        public async Task FromResponseMessage_OperationResult_SessionToken_NullWhenSessionTokenFieldAbsent()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            // Neither sessionToken nor partitionKeyRangeId present
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsNull(response[0].SessionToken,
                "SessionToken must be null when the sessionToken JSON field is absent.");
        }

        // IsRetriable parsing

        [DataTestMethod]
        [Description("IsRetriable is true only when the JSON body has isRetriable as a JSON boolean true; absent, false, or string 'true' must all yield false.")]
        [DataRow(@"{""isRetriable"":true,""operationResponses"":[{""index"":0,""statusCode"":503}]}", true, DisplayName = "JSON boolean true → IsRetriable=true")]
        [DataRow(@"{""isRetriable"":false,""operationResponses"":[{""index"":0,""statusCode"":503}]}", false, DisplayName = "JSON boolean false → IsRetriable=false")]
        [DataRow(@"{""operationResponses"":[{""index"":0,""statusCode"":503}]}", false, DisplayName = "isRetriable absent → IsRetriable=false")]
        [DataRow(@"{""isRetriable"":""true"",""operationResponses"":[{""index"":0,""statusCode"":503}]}", false, DisplayName = "string 'true' (not a JSON boolean) → IsRetriable=false")]
        [DataRow(@"{""IsRetriable"":true,""operationResponses"":[{""index"":0,""statusCode"":503}]}", true, DisplayName = "PascalCase 'IsRetriable' is accepted")]
        public async Task FromResponseMessage_IsRetriable_Parsing(string json, bool expected)
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.ServiceUnavailable, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(expected, response.IsRetriable);
        }

        // DiagnosticString parsing

        [DataTestMethod]
        [Description("DiagnosticString deserializes from the top-level JSON property case-insensitively.")]
        [DataRow("diagnosticString", DisplayName = "camelCase key")]
        [DataRow("DiagnosticString", DisplayName = "PascalCase key")]
        public async Task FromResponseMessage_DiagnosticString_DeserializesCorrectly(string diagnosticStringPropertyName)
        {
            const string expectedDiagnosticString = "TransactionCommitted";
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            string json = $@"{{""{diagnosticStringPropertyName}"":""{expectedDiagnosticString}"",""operationResponses"":[{{""index"":0,""statusCode"":200}}]}}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(expectedDiagnosticString, response.DiagnosticString,
                "DiagnosticString must equal the value from the JSON 'diagnosticString' field.");
            Assert.IsNull(response.ErrorMessage,
                "ErrorMessage must be null on a 200 OK response, even when diagnosticString is present.");
        }

        [TestMethod]
        [Description("DiagnosticString is null when the field is absent from the JSON body.")]
        public async Task FromResponseMessage_DiagnosticString_AbsentInJson_IsNull()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsNull(response.DiagnosticString,
                "DiagnosticString must be null when absent from the JSON body.");
        }

        [TestMethod]
        [Description("Diagnostics is null after FromResponseMessageAsync — the committer is responsible for setting it with the full retry-tree trace.")]
        public async Task FromResponseMessage_Diagnostics_IsNull_BeforeCommitterSetsIt()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":200}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsNull(response.Diagnostics,
                "Diagnostics should be null before the committer sets it with the full retry-tree trace.");
        }

        [TestMethod]
        [Description("ErrorMessage appends DiagnosticString in parentheses when the HTTP error message is also present.")]
        public async Task FromResponseMessage_DiagnosticString_AppendedToErrorMessage()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""diagnosticString"":""TransactionAborted"",""operationResponses"":[{""index"":0,""statusCode"":409}]}";

            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.Conflict, errorMessage: "Transaction was aborted by coordinator")
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(json))
            };

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsTrue(response.ErrorMessage.Contains("Transaction was aborted by coordinator"),
                "ErrorMessage must contain the original HTTP error message.");
            Assert.IsTrue(response.ErrorMessage.Contains("TransactionAborted"),
                "ErrorMessage must contain the coordinator's diagnosticString.");
        }

        [TestMethod]
        [Description("ErrorMessage is set to DiagnosticString alone when the HTTP error message is absent.")]
        public async Task FromResponseMessage_DiagnosticString_UsedAsFallbackErrorMessage()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""diagnosticString"":""LedgerForbidden"",""operationResponses"":[{""index"":0,""statusCode"":403}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.Forbidden, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual("LedgerForbidden", response.ErrorMessage,
                "ErrorMessage must equal DiagnosticString when no HTTP error message is present.");
        }

        [TestMethod]
        [Description("Empty diagnosticString must not produce malformed parentheses in ErrorMessage — treated same as absent.")]
        public async Task FromResponseMessage_DiagnosticString_Empty_DoesNotAppendEmptyParens()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""diagnosticString"":"""",""operationResponses"":[{""index"":0,""statusCode"":409}]}";

            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.Conflict, errorMessage: "Transaction aborted")
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(json))
            };

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsFalse(response.ErrorMessage.Contains("()"),
                "Empty diagnosticString must not produce malformed '()' in ErrorMessage.");
        }

        [DataTestMethod]
        [DataRow("   ", "(   )", DisplayName = "spaces only")]
        [DataRow("\\t", "(\t)", DisplayName = "tab only")]
        [DataRow("\\n", "(\n)", DisplayName = "newline only")]
        [DataRow(" \\t\\r\\n ", "( \t\r\n )", DisplayName = "mixed whitespace")]
        [Description("Whitespace-only diagnosticString must not produce malformed ErrorMessage — treated same as absent.")]
        public async Task FromResponseMessage_DiagnosticString_WhitespaceOnly_DoesNotPollutErrorMessage(
            string jsonEscapedWhitespace,
            string forbiddenAppendix)
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = $@"{{""diagnosticString"":""{jsonEscapedWhitespace}"",""operationResponses"":[{{""index"":0,""statusCode"":409}}]}}";

            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.Conflict, errorMessage: "Transaction aborted")
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(json))
            };

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsTrue(response.ErrorMessage.Contains("Transaction aborted"),
                $"Original HTTP error message must be preserved. Actual: {response.ErrorMessage}");
            Assert.IsFalse(response.ErrorMessage.Contains(forbiddenAppendix),
                $"Whitespace-only diagnosticString must not produce malformed '{forbiddenAppendix}' appendix in ErrorMessage. Actual: {response.ErrorMessage}");
        }

        [TestMethod]
        [Description("Diagnostic strings containing internal whitespace (but non-whitespace characters) must still be merged into ErrorMessage.")]
        public async Task FromResponseMessage_DiagnosticString_WithInternalWhitespace_IsMerged()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            const string diagnostic = "Transaction aborted by coordinator";
            string json = $@"{{""diagnosticString"":""{diagnostic}"",""operationResponses"":[{{""index"":0,""statusCode"":409}}]}}";

            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.Conflict, errorMessage: "HttpFailure")
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(json))
            };

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsTrue(response.ErrorMessage.Contains("HttpFailure"),
                $"Original HTTP error message must be preserved. Actual: {response.ErrorMessage}");
            Assert.IsTrue(response.ErrorMessage.Contains($"({diagnostic})"),
                $"Diagnostic strings with internal whitespace must be merged as '({diagnostic})' into ErrorMessage. Actual: {response.ErrorMessage}");
        }

        [TestMethod]
        [Description("When a 207 MultiStatus (success-range wire status) is promoted to a per-operation error code, the ErrorMessage merge must honor the post-promotion status — i.e. diagnosticString MUST be merged because finalStatusCode is now an error.")]
        public async Task FromResponseMessage_DiagnosticString_MergedAfterMultiStatusPromotion()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 2);
            const string diagnostic = "OperationConflict";

            // Wire status is 207 (success range), but op #1 is 409 — finalStatusCode promotes to 409.
            string json = $@"{{""diagnosticString"":""{diagnostic}"",""operationResponses"":[{{""index"":0,""statusCode"":201}},{{""index"":1,""statusCode"":409}}]}}";

            // Wire ResponseMessage with a 207 MultiStatus and no errorMessage on the wire (success-range responses
            // typically don't carry one). The merge must still kick in based on the *promoted* finalStatusCode (409).
            ResponseMessage responseMessage = new ResponseMessage((HttpStatusCode)StatusCodes.MultiStatus)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(json))
            };

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode,
                "Promotion must move finalStatusCode to 409 (the first failing op).");
            Assert.AreEqual(diagnostic, response.ErrorMessage,
                "Because finalStatusCode is now an error (post-promotion), the diagnosticString must be merged into ErrorMessage.");
        }

        [TestMethod]
        [Description("When a 207 MultiStatus has only successful per-operation results (no promotion), finalStatusCode remains in success range and diagnosticString must NOT pollute ErrorMessage.")]
        public async Task FromResponseMessage_DiagnosticString_NotMerged_WhenMultiStatusHasNoPromotion()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 2);
            const string diagnostic = "TransactionCommitted";

            // Both ops succeed; no promotion — finalStatusCode stays at 207 (success).
            string json = $@"{{""diagnosticString"":""{diagnostic}"",""operationResponses"":[{{""index"":0,""statusCode"":201}},{{""index"":1,""statusCode"":200}}]}}";

            ResponseMessage responseMessage = new ResponseMessage((HttpStatusCode)StatusCodes.MultiStatus)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(json))
            };

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual((HttpStatusCode)StatusCodes.MultiStatus, response.StatusCode,
                "No promotion expected — finalStatusCode must remain 207.");
            Assert.IsNull(response.ErrorMessage,
                "ErrorMessage must remain null on success-range finalStatusCode, even when diagnosticString is present.");
        }



        [TestMethod]
        [Description("Count after Dispose() must not throw — pre-existing safer behavior on main. Returns 0 because Dispose nulls the underlying list.")]
        public async Task Count_AfterDispose_DoesNotThrow()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            response.Dispose();

            int count = response.Count;
            Assert.AreEqual(0, count, "Count must return 0 after disposal without throwing.");
        }

        [TestMethod]
        [Description("GetEnumerator after Dispose() must not throw — pre-existing safer behavior on main. Returns an empty enumerator.")]
        public async Task GetEnumerator_AfterDispose_DoesNotThrow()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            response.Dispose();

            int enumerated = 0;
            foreach (DistributedTransactionOperationResult _ in response)
            {
                enumerated++;
            }

            Assert.AreEqual(0, enumerated, "Enumeration after disposal must yield no items without throwing.");
        }

        // GetOperationResultAtIndex<T>

        [TestMethod]
        [Description("GetOperationResultAtIndex<T> deserializes the resource body into the requested type.")]
        public async Task GetOperationResultAtIndex_WithResourceBody_DeserializesResource()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            // JSON with a resourceBody field that contains a simple document
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":200,""resourceBody"":{""id"":""item1"",""value"":42}}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            DistributedTransactionOperationResult<TestDocument> typed = response.GetOperationResultAtIndex<TestDocument>(0);

            Assert.IsNotNull(typed.Resource, "Resource must be deserialized when a resourceBody is present.");
            Assert.AreEqual("item1", typed.Resource.Id);
            Assert.AreEqual(42, typed.Resource.Value);
        }

        [TestMethod]
        [Description("GetOperationResultAtIndex<T> returns default(T) when the operation has no resource body.")]
        public async Task GetOperationResultAtIndex_WithoutResourceBody_ResourceIsDefault()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":204}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            DistributedTransactionOperationResult<TestDocument> typed = response.GetOperationResultAtIndex<TestDocument>(0);

            Assert.IsNull(typed.Resource, "Resource must be null/default when no resourceBody was returned.");
            Assert.AreEqual(HttpStatusCode.NoContent, typed.StatusCode);
        }

        [TestMethod]
        [Description("GetOperationResultAtIndex<T> preserves all base DistributedTransactionOperationResult fields on the typed wrapper.")]
        public async Task GetOperationResultAtIndex_PreservesBaseFields()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);

            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":200,""etag"":""\""abc\"""",""requestCharge"":1.5,""resourceBody"":{""id"":""x"",""value"":1}}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            DistributedTransactionOperationResult<TestDocument> typed = response.GetOperationResultAtIndex<TestDocument>(0);

            Assert.AreEqual(HttpStatusCode.OK, typed.StatusCode);
            Assert.AreEqual("\"abc\"", typed.ETag);
            Assert.AreEqual(1.5, typed.RequestCharge);
        }

        [TestMethod]
        [Description("GetOperationResultAtIndex<T> after Dispose throws ObjectDisposedException.")]
        public async Task GetOperationResultAtIndex_AfterDispose_ThrowsObjectDisposedException()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":200}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            response.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() => response.GetOperationResultAtIndex<TestDocument>(0));
        }

        [TestMethod]
        [Description("GetOperationResultAtIndex<T> with an out-of-range index throws ArgumentOutOfRangeException.")]
        public async Task GetOperationResultAtIndex_OutOfRangeIndex_ThrowsArgumentOutOfRangeException()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":200}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => response.GetOperationResultAtIndex<TestDocument>(99));
        }

        [TestMethod]
        [Description("GetOperationResultAtIndex<T> can be invoked repeatedly on the same index and must yield the same deserialized resource each time without disposing the underlying ResourceStream.")]
        public async Task GetOperationResultAtIndex_CalledTwice_BothCallsSucceedAndStreamRemainsReadable()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":200,""resourceBody"":{""id"":""dup"",""value"":7}}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            DistributedTransactionOperationResult<TestDocument> first = response.GetOperationResultAtIndex<TestDocument>(0);
            DistributedTransactionOperationResult<TestDocument> second = response.GetOperationResultAtIndex<TestDocument>(0);

            Assert.IsNotNull(first.Resource);
            Assert.IsNotNull(second.Resource);
            Assert.AreEqual("dup", first.Resource.Id);
            Assert.AreEqual("dup", second.Resource.Id);
            Assert.AreEqual(7, first.Resource.Value);
            Assert.AreEqual(7, second.Resource.Value);

            // After two typed reads, the original ResourceStream must still be readable.
            using StreamReader reader = new StreamReader(response[0].ResourceStream);
            string raw = reader.ReadToEnd();
            StringAssert.Contains(raw, "\"id\":\"dup\"", "Underlying ResourceStream must remain readable after GetOperationResultAtIndex<T>.");
        }

        [TestMethod]
        [Description("Calling Dispose() after GetOperationResultAtIndex<T> must still successfully release the ResourceStream and must be safe to call multiple times.")]
        public async Task GetOperationResultAtIndex_FollowedByDispose_DoesNotThrow()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":200,""resourceBody"":{""id"":""z"",""value"":1}}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            _ = response.GetOperationResultAtIndex<TestDocument>(0);

            response.Dispose();
            response.Dispose(); // must remain idempotent

            Assert.ThrowsException<ObjectDisposedException>(() => response.GetOperationResultAtIndex<TestDocument>(0));
        }

        [TestMethod]
        [Description("GetOperationResultAtIndex<T> throws InvalidOperationException when no serializer is available but a resource body is present.")]
        public async Task GetOperationResultAtIndex_NullSerializer_WithResourceBody_ThrowsInvalidOperationException()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":200,""resourceBody"":{""id"":""x"",""value"":1}}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                serializer: null,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.ThrowsException<InvalidOperationException>(
                () => response.GetOperationResultAtIndex<TestDocument>(0));
        }

        [TestMethod]
        [Description("GetOperationResultAtIndex<T> with a null serializer returns default(T) when there is no resource body to deserialize.")]
        public async Task GetOperationResultAtIndex_NullSerializer_WithoutResourceBody_ReturnsDefault()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":204}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                serializer: null,
                NoOpTrace.Singleton,
                CancellationToken.None);

            DistributedTransactionOperationResult<TestDocument> typed = response.GetOperationResultAtIndex<TestDocument>(0);

            Assert.IsNull(typed.Resource);
        }

        // Non-seekable response content / stream buffering (Negative Tests.md - Response Content section)
        //
        // FromResponseMessageAsync must buffer a non-seekable response stream into a seekable
        // MemoryStream (DistributedTransactionResponse.cs L239-245) before parsing, so that every
        // parse path (success, per-op error, malformed JSON, 207 promotion) behaves identically
        // whether or not the transport handed back a seekable stream. The buffered MemoryStream is
        // disposed in the finally block (L295); the returned response must remain usable afterwards
        // because it holds the parsed results, not the stream.
        //
        // NOTE: mid-copy cancellation is intentionally NOT tested. The source calls
        // Content.CopyToAsync(memoryStream) WITHOUT a CancellationToken (L242), so the SDK does not
        // honor cancellation during buffering. Only the entry-point ThrowIfCancellationRequested
        // (L224) is observable and is covered below.

        [TestMethod]
        [Description("A seekable stream positioned at 0 (the common transport case) parses successfully without rewinding.")]
        public async Task FromResponseMessage_SeekableContent_AtPositionZero_ParsesSuccessfully()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            Assert.IsTrue(responseMessage.Content.CanSeek);
            Assert.AreEqual(0, responseMessage.Content.Position);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual(1, response.Count);
            Assert.AreEqual(HttpStatusCode.Created, response[0].StatusCode);
        }

        [TestMethod]
        [Description("A seekable stream positioned at EOF is parsed from its current position (the SDK does not rewind seekable streams), so the body reads as empty. On a success status this surfaces as InternalServerError (invalid server response), locking down the no-rewind behavior.")]
        public async Task FromResponseMessage_SeekableContent_AtPositionEnd_SuccessStatus_ReturnsInternalServerError()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            Assert.IsTrue(responseMessage.Content.CanSeek);

            // Position the seekable stream at EOF. The SDK only buffers/rewinds non-seekable
            // streams, so a seekable stream is parsed from its current position -> empty read.
            responseMessage.Content.Seek(0, SeekOrigin.End);
            Assert.AreEqual(responseMessage.Content.Length, responseMessage.Content.Position);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode,
                "A success status with an empty (EOF) body must surface as InternalServerError because the server contract requires a parseable body on success.");
            Assert.IsFalse(response.IsSuccessStatusCode);
        }

        [TestMethod]
        [Description("A non-seekable success response is buffered into a seekable stream and parsed, yielding the per-op results.")]
        public async Task FromResponseMessage_NonSeekableContent_SuccessStatus_BuffersAndParses()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201}]}";
            ResponseMessage responseMessage = BuildNonSeekableResponseMessage(HttpStatusCode.OK, json);

            Assert.IsFalse(responseMessage.Content.CanSeek, "Test setup must provide a non-seekable stream.");

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual(1, response.Count);
            Assert.AreEqual(HttpStatusCode.Created, response[0].StatusCode);
        }

        [TestMethod]
        [Description("A non-seekable error response is buffered and parsed, surfacing the per-operation error status.")]
        public async Task FromResponseMessage_NonSeekableContent_ErrorStatus_SurfacesPerOpError()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":409}]}";
            ResponseMessage responseMessage = BuildNonSeekableResponseMessage(HttpStatusCode.Conflict, json);

            Assert.IsFalse(responseMessage.Content.CanSeek);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.AreEqual(1, response.Count);
            Assert.AreEqual(HttpStatusCode.Conflict, response[0].StatusCode);
        }

        [TestMethod]
        [Description("A non-seekable response with malformed JSON and a success status is buffered, fails to deserialize, and surfaces 500 with a deserialization-failure message.")]
        public async Task FromResponseMessage_NonSeekableContent_MalformedJson_SuccessStatus_ReturnsInternalServerError()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            ResponseMessage responseMessage = BuildNonSeekableResponseMessage(HttpStatusCode.OK, "{invalid-json");

            Assert.IsFalse(responseMessage.Content.CanSeek);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.AreEqual(ClientResources.ServerResponseDeserializationFailure, response.ErrorMessage);
        }

        [TestMethod]
        [Description("A non-seekable response with malformed JSON and an error status is buffered and its results are padded with the error status code.")]
        public async Task FromResponseMessage_NonSeekableContent_MalformedJson_ErrorStatus_PadsResults()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 2);
            ResponseMessage responseMessage = BuildNonSeekableResponseMessage(HttpStatusCode.Conflict, "{invalid-json");

            Assert.IsFalse(responseMessage.Content.CanSeek);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
            Assert.AreEqual(2, response.Count);

            for (int i = 0; i < response.Count; i++)
            {
                Assert.AreEqual(HttpStatusCode.Conflict, response[i].StatusCode);
            }
        }

        [TestMethod]
        [Description("A non-seekable 207 MultiStatus response is buffered and promotes the first non-dependency failure to the overall status, identically to the seekable path.")]
        public async Task FromResponseMessage_NonSeekableContent_MultiStatus_PromotesFirstFailure()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 2);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201},{""index"":1,""statusCode"":409}]}";
            ResponseMessage responseMessage = BuildNonSeekableResponseMessage((HttpStatusCode)StatusCodes.MultiStatus, json);

            Assert.IsFalse(responseMessage.Content.CanSeek);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode,
                "Non-seekable 207 must promote to the first failing op (409), matching seekable behavior.");
            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.AreEqual(2, response.Count);
        }

        [TestMethod]
        [Description("A pre-cancelled CancellationToken short-circuits at the entry point and throws OperationCanceledException before any parsing.")]
        public async Task FromResponseMessage_PreCancelledToken_ThrowsOperationCanceled()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201}]}";
            ResponseMessage responseMessage = BuildResponseMessage(HttpStatusCode.OK, json);

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => DistributedTransactionResponse.FromResponseMessageAsync(
                    responseMessage,
                    serverRequest,
                    MockCosmosUtil.Serializer,
                    NoOpTrace.Singleton,
                    cts.Token));
        }

        [TestMethod]
        [Description("After a non-seekable response is parsed, the internally buffered MemoryStream is disposed but the returned response remains fully usable (Count, indexer, status).")]
        public async Task FromResponseMessage_NonSeekableContent_InternalBufferDisposed_ResponseRemainsUsable()
        {
            DistributedTransactionServerRequest serverRequest = await BuildServerRequestAsync(operationCount: 1);
            string json = @"{""operationResponses"":[{""index"":0,""statusCode"":201}]}";
            ResponseMessage responseMessage = BuildNonSeekableResponseMessage(HttpStatusCode.OK, json);

            DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                responseMessage,
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);

            // The internal buffer is disposed in the finally block; the response must still be readable.
            Assert.AreEqual(1, response.Count);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(HttpStatusCode.Created, response[0].StatusCode);
        }

        // Helpers

        private sealed class TestDocument
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string Id { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("value")]
            public int Value { get; set; }
        }

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
                    partitionKey: new PartitionKey($"pk{i}"),
                    id: $"doc{i}"));
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
        /// Builds a <see cref="ResponseMessage"/> whose content stream is NOT seekable, forcing
        /// <see cref="DistributedTransactionResponse.FromResponseMessageAsync"/> to buffer it into a
        /// seekable <see cref="MemoryStream"/> before parsing.
        /// </summary>
        private static ResponseMessage BuildNonSeekableResponseMessage(HttpStatusCode statusCode, string json)
        {
            return new ResponseMessage(statusCode)
            {
                Content = new NonSeekableStream(Encoding.UTF8.GetBytes(json))
            };
        }

        /// <summary>
        /// A forward-only, non-seekable read stream backed by a byte buffer. Mirrors the kind of
        /// stream the transport may hand back when a response is not seekable.
        /// </summary>
        private sealed class NonSeekableStream : Stream
        {
            private readonly MemoryStream inner;

            public NonSeekableStream(byte[] buffer)
            {
                this.inner = new MemoryStream(buffer);
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count) => this.inner.Read(buffer, offset, count);

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => this.inner.ReadAsync(buffer, offset, count, cancellationToken);

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.inner.Dispose();
                }

                base.Dispose(disposing);
            }
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
                json = $@"{{""operationResponses"":[{{""index"":0,""statusCode"":{(int)statusCode}}}]}}";
            }
            else
            {
                List<string> results = new List<string>();
                for (int i = 0; i < operationCount; i++)
                {
                    results.Add($@"{{""index"":{i},""statusCode"":{(int)statusCode}}}");
                }
                json = $@"{{""operationResponses"":[{string.Join(",", results)}]}}";
            }

            return await DistributedTransactionResponse.FromResponseMessageAsync(
                BuildResponseMessage(statusCode, json),
                serverRequest,
                MockCosmosUtil.Serializer,
                NoOpTrace.Singleton,
                CancellationToken.None);
        }
    }
}
