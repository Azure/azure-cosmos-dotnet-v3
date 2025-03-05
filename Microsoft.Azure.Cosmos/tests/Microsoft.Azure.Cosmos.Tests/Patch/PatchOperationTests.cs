//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Mail;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PatchOperationTests
    {
        private const string path = "/random";

        [TestMethod]
        public void ThrowsOnNullArguement()
        {
            try
            {
                PatchOperation.Add(null, "1");
                Assert.Fail();
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual(ex.ParamName, "path");
            }

            try
            {
                PatchOperation.Remove(null);
                Assert.Fail();
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual(ex.ParamName, "path");
            }
        }

        [TestMethod]
        public void ConstructPatchOperationTest()
        {
            PatchOperation operation = PatchOperation.Add(path, "string");
            PatchOperationTests.ValidateOperations(operation, PatchOperationType.Add, "string");

            DateTime current = DateTime.UtcNow;
            operation = PatchOperation.Add(path, current);
            PatchOperationTests.ValidateOperations(operation, PatchOperationType.Add, current);

            dynamic complexObject = new { a = "complex", b = 12.34, c = true };
            operation = PatchOperation.Add(path, complexObject);
            PatchOperationTests.ValidateOperations(operation, PatchOperationType.Add, complexObject);

            operation = PatchOperation.Remove(path);
            PatchOperationTests.ValidateOperations(operation, PatchOperationType.Remove, "value not required");

            int[] arrayObject = { 1, 2, 3 };
            operation = PatchOperation.Replace(path, arrayObject);
            PatchOperationTests.ValidateOperations(operation, PatchOperationType.Replace, arrayObject);

            Guid guid = new Guid();
            operation = PatchOperation.Set(path, guid);
            PatchOperationTests.ValidateOperations(operation, PatchOperationType.Set, guid);

            operation = PatchOperation.Set<object>(path, null);
            PatchOperationTests.ValidateOperations<object>(operation, PatchOperationType.Set, null);
        }

        [TestMethod]
        [DataRow(false, true, DisplayName = "Test scenario when binary encoding is disabled at client level and supported in container level.")]
        [DataRow(true, true, DisplayName = "Test scenario when binary encoding is enabled at client level and supported in container level.")]
        [DataRow(false, false, DisplayName = "Test scenario when binary encoding iis disabled at client level and disabled in container level.")]
        [DataRow(true, false, DisplayName = "Test scenario when binary encoding is enabled at client level and disabled in container level.")]
        public async Task VerifyPatchItemOperation(
            bool binaryEncodingEnabledInContainer,
            bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                ItemRequestOptions requestOptions = null;
                HttpStatusCode httpStatusCode = HttpStatusCode.OK;
                int testHandlerHitCount = 0;
                TestHandler testHandler = new TestHandler((request, cancellationToken) =>
                {
                    Assert.IsTrue(request.RequestUri.OriginalString.StartsWith(@"dbs/testdb/colls/testcontainer/docs/cdbBinaryIdRequest"));
                    Assert.AreEqual(new PartitionKey("FF627B77-568E-4541-A47E-041EAC10E46F").ToString(), request.Headers.PartitionKey);
                    Assert.AreEqual(Documents.ResourceType.Document, request.ResourceType);
                    Assert.AreEqual(Documents.OperationType.Patch, request.OperationType);
                    Assert.AreEqual(requestOptions, request.RequestOptions);
                    testHandlerHitCount++;

                    return Task.FromResult(
                        PatchOperationTests.GetContainerItemResponse(
                            request,
                            httpStatusCode,
                            binaryEncodingEnabledInContainer));
                });

                CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(
                    (builder) => builder.AddCustomHandlers(testHandler));

                Container container = client.GetDatabase("testdb")
                                            .GetContainer("testcontainer");

                ContainerInternal containerInternal = (ContainerInternal)container;

                dynamic testItem = new
                {
                    id = Guid.NewGuid().ToString(),
                    pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
                };

                List<PatchOperation> patchOperations = new List<PatchOperation>()
                {
                    PatchOperation.Add("/name", "replaced_name")
                };

                ItemResponse<dynamic> responseMessage = await containerInternal.PatchItemAsync<dynamic>(
                    id: "cdbBinaryIdRequest",
                    partitionKey: new Cosmos.PartitionKey(testItem.pk),
                    patchOperations: patchOperations);

                Assert.IsNotNull(responseMessage);
                Assert.AreEqual(httpStatusCode, responseMessage.StatusCode);
                Assert.AreEqual(1, testHandlerHitCount, "The operation did not make it to the handler");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        [DataRow(false, true, DisplayName = "Test scenario when binary encoding is disabled at client level and supported in container level.")]
        [DataRow(true, true, DisplayName = "Test scenario when binary encoding is enabled at client level and supported in container level.")]
        [DataRow(false, false, DisplayName = "Test scenario when binary encoding iis disabled at client level and disabled in container level.")]
        [DataRow(true, false, DisplayName = "Test scenario when binary encoding is enabled at client level and disabled in container level.")]
        public async Task VerifyPatchItemStreamOperation(
            bool binaryEncodingEnabledInContainer,
            bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                dynamic testItem = new
                {
                    id = Guid.NewGuid().ToString(),
                    pk = "FF627B77-568E-4541-A47E-041EAC10E46F",
                };

                using Stream itemStream = MockCosmosUtil.Serializer.ToStream<dynamic>(testItem);

                ItemRequestOptions requestOptions = null;
                HttpStatusCode httpStatusCode = HttpStatusCode.OK;
                int testHandlerHitCount = 0;
                TestHandler testHandler = new TestHandler((request, cancellationToken) =>
                {
                    Assert.IsTrue(request.RequestUri.OriginalString.StartsWith(@"dbs/testdb/colls/testcontainer/docs/cdbBinaryIdRequest"));
                    Assert.AreEqual(PartitionKey.Null.ToJsonString(), request.Headers.PartitionKey);
                    Assert.AreEqual(Documents.ResourceType.Document, request.ResourceType);
                    Assert.AreEqual(Documents.OperationType.Patch, request.OperationType);
                    Assert.AreEqual(requestOptions, request.RequestOptions);
                    testHandlerHitCount++;

                    return Task.FromResult(
                        PatchOperationTests.GetContainerItemResponse(
                            request,
                            httpStatusCode,
                            binaryEncodingEnabledInContainer));
                });

                CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(
                    (builder) => builder.AddCustomHandlers(testHandler));

                Container container = client.GetDatabase("testdb")
                                            .GetContainer("testcontainer");

                ContainerInternal containerInternal = (ContainerInternal)container;
                ResponseMessage responseMessage = await containerInternal.PatchItemStreamAsync(
                    id: "cdbBinaryIdRequest",
                    partitionKey: Cosmos.PartitionKey.Null,
                    streamPayload: itemStream,
                    requestOptions: requestOptions);
                Assert.IsNotNull(responseMessage);
                Assert.AreEqual(httpStatusCode, responseMessage.StatusCode);
                Assert.AreEqual(1, testHandlerHitCount, "The operation did not make it to the handler");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        private static void ValidateOperations<T>(PatchOperation patchOperation, PatchOperationType operationType, T value)
        {
            Assert.AreEqual(operationType, patchOperation.OperationType);
            Assert.AreEqual(path, patchOperation.Path);

            if (!operationType.Equals(PatchOperationType.Remove))
            {
                string expected;
                CosmosSerializer cosmosSerializer = new CosmosJsonDotNetSerializer();
                using (Stream stream = cosmosSerializer.ToStream(value))
                {
                    using (StreamReader streamReader = new StreamReader(stream))
                    {
                        expected = streamReader.ReadToEnd();
                    }
                }

                Assert.IsTrue(patchOperation.TrySerializeValueParameter(new CustomSerializer(), out Stream valueParam));

                string actual;
                using (valueParam)
                {
                    using (StreamReader streamReader = new StreamReader(valueParam))
                    {
                        actual = streamReader.ReadToEnd();
                    }
                }

                Assert.AreEqual(expected, actual);
            }
        }

        private static ResponseMessage GetContainerItemResponse(
            RequestMessage request,
            HttpStatusCode httpStatusCode,
            bool binaryEncodingEnabledInContainer)
        {
            string itemResponseString = "{\r\n    \\\"id\\\": \\\"60362d85-ce1e-4ceb-9af3-f2ddfebf4547\\\",\r\n    \\\"pk\\\": \\\"pk\\\",\r\n    \\\"name\\\": \\\"1856531480\\\",\r\n    " +
                    "\\\"email\\\": \\\"dkunda@test.com\\\",\r\n    \\\"body\\\": \\\"This document is intended for binary encoding test.\\\",\r\n    \\\"_rid\\\": \\\"fIsUAKsjjj0BAAAAAAAAAA==\\\",\r\n    " +
                    "\\\"_self\\\": \\\"dbs/fIsUAA==/colls/fIsUAKsjjj0=/docs/fIsUAKsjjj0BAAAAAAAAAA==/\\\",\r\n    \\\"_etag\\\": \\\"\\\\\"510096bc-0000-0d00-0000-66ccf70b0000\\\\\"\\\",\r\n    " +
                    "\\\"_attachments\\\": \\\"attachments/\\\",\r\n    \\\"_ts\\\": 1724708619\r\n}";

            bool shouldReturnBinaryResponse = binaryEncodingEnabledInContainer
                && request.Headers[Documents.HttpConstants.HttpHeaders.SupportedSerializationFormats] != null
                && request.Headers[Documents.HttpConstants.HttpHeaders.SupportedSerializationFormats].Equals(Documents.SupportedSerializationFormats.CosmosBinary.ToString());

            return new ResponseMessage(httpStatusCode, request, errorMessage: null)
            {
                Content = shouldReturnBinaryResponse
                ? CosmosSerializerUtils.ConvertInputToBinaryStream(
                    itemResponseString,
                    Newtonsoft.Json.JsonSerializer.Create())
                : CosmosSerializerUtils.ConvertInputToTextStream(
                    itemResponseString,
                    Newtonsoft.Json.JsonSerializer.Create())
            };
        }

        private class CustomSerializer : CosmosSerializer
        {
            private readonly CosmosSerializer cosmosSerializer = new CosmosJsonDotNetSerializer();

            public override T FromStream<T>(Stream stream)
            {
                return this.cosmosSerializer.FromStream<T>(stream);
            }

            public override Stream ToStream<T>(T input)
            {
                return this.cosmosSerializer.ToStream(input);
            }
        }
    }
}