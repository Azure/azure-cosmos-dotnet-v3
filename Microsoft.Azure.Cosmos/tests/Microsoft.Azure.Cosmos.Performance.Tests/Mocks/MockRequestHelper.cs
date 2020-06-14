//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System;
    using System.IO;
    using Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal static class MockRequestHelper
    {
        internal static readonly byte[] testItemResponsePayload;
        internal static readonly byte[] testItemFeedResponsePayload;

        static MockRequestHelper()
        {
            MemoryStream ms = new MemoryStream();
            using (FileStream fs = File.OpenRead("samplepayload.json"))
            {
                fs.CopyTo(ms);
                MockRequestHelper.testItemResponsePayload = ms.ToArray();
            }

            ms = new MemoryStream();
            using (FileStream fs = File.OpenRead("samplefeedpayload.json"))
            {
                fs.CopyTo(ms);
                MockRequestHelper.testItemFeedResponsePayload = ms.ToArray();
            }
        }

        /// <summary>
        /// For mocking a Gateway response
        /// </summary>
        /// <param name="request">The <see cref="DocumentServiceRequest"/> instance.</param>
        /// <returns>A <see cref="DocumentServiceResponse"/> instance.</returns>
        public static DocumentServiceResponse GetDocumentServiceResponse(DocumentServiceRequest request)
        {
            DictionaryNameValueCollection headers = MockRequestHelper.GenerateTestHeaders();

            if (request.OperationType == OperationType.Read)
            {
                if (request.ResourceAddress.EndsWith(MockedItemBenchmarkHelper.ExistingItemId))
                {
                    return new DocumentServiceResponse(
                        new MemoryStream(MockRequestHelper.testItemResponsePayload),
                        headers,
                        System.Net.HttpStatusCode.OK
                    );
                }

                return new DocumentServiceResponse(
                    Stream.Null,
                    headers,
                    System.Net.HttpStatusCode.NotFound
                );
            }

            if (request.OperationType == OperationType.Delete)
            {
                if (request.ResourceAddress.EndsWith(MockedItemBenchmarkHelper.ExistingItemId))
                {
                    return new DocumentServiceResponse(
                        new MemoryStream(MockRequestHelper.testItemResponsePayload),
                        headers,
                        System.Net.HttpStatusCode.OK
                    );
                }

                return new DocumentServiceResponse(
                    Stream.Null,
                    headers,
                    System.Net.HttpStatusCode.NotFound
                );
            }

            if (request.OperationType == OperationType.Create
                || request.OperationType == OperationType.Replace
                || request.OperationType == OperationType.Upsert
                || request.OperationType == OperationType.Patch)
            {
                return new DocumentServiceResponse(
                        new MemoryStream(MockRequestHelper.testItemResponsePayload),
                        headers,
                        System.Net.HttpStatusCode.OK
                    );
            }

            if (request.ResourceType == ResourceType.Document &&
                request.OperationType == OperationType.ReadFeed)
            {
                return new DocumentServiceResponse(
                        new MemoryStream(MockRequestHelper.testItemFeedResponsePayload),
                        headers,
                        System.Net.HttpStatusCode.OK
                    );
            }

            return null;
        }

        /// <summary>
        /// For mocking a TransportClient response/
        /// </summary>
        /// <param name="request">The <see cref="DocumentServiceRequest"/> instance.</param>
        /// <returns>A <see cref="StoreResponse"/> instance.</returns>
        public static StoreResponse GetStoreResponse(DocumentServiceRequest request)
        {
            DictionaryNameValueCollection headers = MockRequestHelper.GenerateTestHeaders();

            if (request.OperationType == OperationType.Read)
            {
                headers.Add(WFConstants.BackendHeaders.LSN, "1");
                if (request.ResourceAddress.EndsWith(MockedItemBenchmarkHelper.ExistingItemId))
                {

                    return new StoreResponse()
                    {
                        ResponseBody = new MemoryStream(MockRequestHelper.testItemResponsePayload, 0, MockRequestHelper.testItemResponsePayload.Length, writable: false, publiclyVisible: true),
                        Status = (int)System.Net.HttpStatusCode.OK,
                        Headers = headers,
                    };
                }

                return new StoreResponse()
                {
                    ResponseBody = Stream.Null,
                    Status = (int)System.Net.HttpStatusCode.NotFound,
                    Headers = headers,
                };
            }

            if (request.OperationType == OperationType.Delete)
            {
                if (request.ResourceAddress.EndsWith(MockedItemBenchmarkHelper.ExistingItemId))
                {
                    return new StoreResponse()
                    {
                        ResponseBody = new MemoryStream(MockRequestHelper.testItemResponsePayload, 0, MockRequestHelper.testItemResponsePayload.Length, writable: false, publiclyVisible: true),
                        Status = (int)System.Net.HttpStatusCode.OK,
                        Headers = headers,
                    };
                }

                return new StoreResponse()
                {
                    ResponseBody = Stream.Null,
                    Status = (int)System.Net.HttpStatusCode.NotFound,
                    Headers = headers,
                };
            }

            if (request.OperationType == OperationType.Create
                || request.OperationType == OperationType.Replace
                || request.OperationType == OperationType.Upsert
                || request.OperationType == OperationType.Patch)
            {
                return new StoreResponse()
                {
                    ResponseBody = new MemoryStream(MockRequestHelper.testItemResponsePayload, 0, MockRequestHelper.testItemResponsePayload.Length, writable: false, publiclyVisible: true),
                    Status = (int)System.Net.HttpStatusCode.OK,
                    Headers = headers,
                };
            }

            if (request.ResourceType == ResourceType.Document &&
                request.OperationType == OperationType.ReadFeed)
            {
                return new StoreResponse()
                {
                    ResponseBody = new MemoryStream(MockRequestHelper.testItemFeedResponsePayload, 0, MockRequestHelper.testItemFeedResponsePayload.Length, writable: false, publiclyVisible: true),
                    Status = (int)System.Net.HttpStatusCode.OK,
                    Headers = headers,
                };
            }

            return null;
        }

        private static DictionaryNameValueCollection GenerateTestHeaders()
        {
            DictionaryNameValueCollection headers = new DictionaryNameValueCollection();
            for (int i = 0; i < 15; i++)
            {
                string random = Guid.NewGuid().ToString();
                headers[random] = random;
            }

            return headers;
        }
    }
}
