//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System;
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
        public static Task<DocumentServiceResponse> GetDocumentServiceResponse(DocumentServiceRequest request)
        {
            DocumentServiceResponse response = null;
            if (request.OperationType == OperationType.Read)
            {
                if (request.ResourceAddress.EndsWith(Constants.ValidOperationId))
                {
                    response = new DocumentServiceResponse(
                        new MemoryStream(MockRequestHelper.testPayload),
                        new DictionaryNameValueCollection(),
                        System.Net.HttpStatusCode.OK
                    );
                }
                else
                {
                    response = new DocumentServiceResponse(
                        Stream.Null,
                        new DictionaryNameValueCollection(),
                        System.Net.HttpStatusCode.NotFound
                    );
                }
            }

            if (request.OperationType == OperationType.Delete)
            {
                if (request.ResourceAddress.EndsWith(Tests.Constants.ValidOperationId))
                {
                    response = new DocumentServiceResponse(
                        new MemoryStream(MockRequestHelper.testPayload),
                        new DictionaryNameValueCollection(),
                        System.Net.HttpStatusCode.OK
                    );
                }
                else
                {
                    response = new DocumentServiceResponse(
                        Stream.Null,
                        new DictionaryNameValueCollection(),
                        System.Net.HttpStatusCode.NotFound
                    );
                }
            }

            if (request.OperationType == OperationType.Create 
                || request.OperationType == OperationType.Replace 
                || request.OperationType == OperationType.Upsert 
                || request.OperationType == OperationType.Patch)
            {
                response = new DocumentServiceResponse(
                        new MemoryStream(MockRequestHelper.testPayload),
                        new DictionaryNameValueCollection(),
                        System.Net.HttpStatusCode.OK
                    );
            }

            return Task.FromResult(response);
        }

        /// <summary>
        /// For mocking a TransportClient response/
        /// </summary>
        /// <param name="request">The <see cref="DocumentServiceRequest"/> instance.</param>
        /// <returns>A <see cref="StoreResponse"/> instance.</returns>
        public static Task<StoreResponse> GetStoreResponse(DocumentServiceRequest request)
        {
            StoreResponse response = null;
            if (request.OperationType == OperationType.Read)
            {
                if (request.ResourceAddress.EndsWith(Constants.ValidOperationId))
                {
                    response = new StoreResponse()
                    {
                        ResponseBody = new MemoryStream(MockRequestHelper.testPayload),
                        Status = (int)System.Net.HttpStatusCode.OK,
                        ResponseHeaderNames = new string[] { WFConstants.BackendHeaders.LSN },
                        ResponseHeaderValues = new string[] { "1" }
                    };
                }
                else
                {
                    response = new StoreResponse()
                    {
                        ResponseBody = Stream.Null,
                        Status = (int)System.Net.HttpStatusCode.NotFound,
                        ResponseHeaderNames = new string[] { WFConstants.BackendHeaders.LSN },
                        ResponseHeaderValues = new string[] { "1" }
                    };
                }
            }

            if (request.OperationType == OperationType.Delete)
            {
                if (request.ResourceAddress.EndsWith(Constants.ValidOperationId))
                {
                    response = new StoreResponse()
                    {
                        ResponseBody = new MemoryStream(MockRequestHelper.testPayload),
                        Status = (int)System.Net.HttpStatusCode.OK,
                        ResponseHeaderNames = Array.Empty<string>(),
                        ResponseHeaderValues = Array.Empty<string>()
                    };
                }
                else
                {
                    response = new StoreResponse()
                    {
                        ResponseBody = Stream.Null,
                        Status = (int)System.Net.HttpStatusCode.NotFound,
                        ResponseHeaderNames = Array.Empty<string>(),
                        ResponseHeaderValues = Array.Empty<string>()
                    };
                }
            }

            if (request.OperationType == OperationType.Create
                || request.OperationType == OperationType.Replace
                || request.OperationType == OperationType.Upsert
                || request.OperationType == OperationType.Patch)
            {
                response = new StoreResponse()
                {
                    ResponseBody = new MemoryStream(MockRequestHelper.testPayload),
                    Status = (int)System.Net.HttpStatusCode.OK,
                    ResponseHeaderNames = Array.Empty<string>(),
                    ResponseHeaderValues = Array.Empty<string>()
                };
            }

            return Task.FromResult(response);
        }
    }
}
