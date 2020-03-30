//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal static class MockRequestHelper
    {
        private static readonly byte[] testPayload;

        static MockRequestHelper()
        {
            MemoryStream ms = new MemoryStream();
            using (FileStream fs = File.OpenRead("samplepayload.json"))
            {
                fs.CopyTo(ms);
                MockRequestHelper.testPayload = ms.ToArray();
            }
        }

        /// <summary>
        /// For mocking a Gateway response
        /// </summary>
        /// <param name="request">The <see cref="DocumentServiceRequest"/> instance.</param>
        /// <returns>A <see cref="DocumentServiceResponse"/> instance.</returns>
        public static DocumentServiceResponse GetDocumentServiceResponse(DocumentServiceRequest request)
        {
            if (request.OperationType == OperationType.Read)
            {
                if (request.ResourceAddress.EndsWith(Constants.ValidOperationId))
                {
                    return new DocumentServiceResponse(
                        new MemoryStream(MockRequestHelper.testPayload),
                        new DictionaryNameValueCollection(),
                        System.Net.HttpStatusCode.OK
                    );
                }

                return new DocumentServiceResponse(
                    Stream.Null,
                    new DictionaryNameValueCollection(),
                    System.Net.HttpStatusCode.NotFound
                );
            }

            if (request.OperationType == OperationType.Delete)
            {
                if (request.ResourceAddress.EndsWith(Tests.Constants.ValidOperationId))
                {
                    return new DocumentServiceResponse(
                        new MemoryStream(MockRequestHelper.testPayload),
                        new DictionaryNameValueCollection(),
                        System.Net.HttpStatusCode.OK
                    );
                }

                return new DocumentServiceResponse(
                    Stream.Null,
                    new DictionaryNameValueCollection(),
                    System.Net.HttpStatusCode.NotFound
                );
            }

            if (request.OperationType == OperationType.Create
                || request.OperationType == OperationType.Replace
                || request.OperationType == OperationType.Upsert
                || request.OperationType == OperationType.Patch)
            {
                return new DocumentServiceResponse(
                        new MemoryStream(MockRequestHelper.testPayload),
                        new DictionaryNameValueCollection(),
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
            DictionaryNameValueCollection headers = new DictionaryNameValueCollection();
            if (request.OperationType == OperationType.Read)
            {
                headers.Add(WFConstants.BackendHeaders.LSN, "1");
                if (request.ResourceAddress.EndsWith(Constants.ValidOperationId))
                {

                    return new StoreResponse()
                    {
                        ResponseBody = new MemoryStream(MockRequestHelper.testPayload),
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
                if (request.ResourceAddress.EndsWith(Constants.ValidOperationId))
                {
                    return new StoreResponse()
                    {
                        ResponseBody = new MemoryStream(MockRequestHelper.testPayload),
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
                    ResponseBody = new MemoryStream(MockRequestHelper.testPayload),
                    Status = (int)System.Net.HttpStatusCode.OK,
                    Headers = headers,
                };
            }

            return null;
        }
    }
}
