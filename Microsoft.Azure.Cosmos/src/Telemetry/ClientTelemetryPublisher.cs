//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    internal class ClientTelemetryPublisher
    {
        internal static ResponseMessage Publish(
          Documents.OperationType operationType,
          Documents.ResourceType resourceType,
          RequestOptions requestOptions,
          Container container,
          ResponseMessage response)
        {
            string consistencyValue = null;
            if (requestOptions != null && requestOptions.BaseConsistencyLevel.HasValue)
            {
                consistencyValue = requestOptions.BaseConsistencyLevel.ToString();
            }

            ClientTelemetry.Collect(
                cosmosDiagnostics: response.Diagnostics,
                statusCode: response.StatusCode,
                responseSizeInBytes: GetPayloadSize(response),
                databaseId: container.Database.Id,
                containerId: container.Id,
                operationType: operationType,
                resourceType: resourceType,
                consistencyLevel: consistencyValue,
                requestCharge: response.Headers.RequestCharge);

            return response;
        }

        internal static ItemResponse<T> Publish<T>(
            Documents.OperationType operationType,
            Documents.ResourceType resourceType,
            RequestOptions requestOptions,
            Container container,
            ItemResponse<T> response)
        {
            string consistencyValue = null;
            if (requestOptions != null && requestOptions.BaseConsistencyLevel.HasValue)
            {
                consistencyValue = requestOptions.BaseConsistencyLevel.ToString();
            }

            ClientTelemetry.Collect(
                cosmosDiagnostics: response.Diagnostics,
                statusCode: response.StatusCode,
                responseSizeInBytes: GetPayloadSize(response),
                databaseId: container.Database.Id,
                containerId: container.Id,
                operationType: operationType,
                resourceType: resourceType,
                consistencyLevel: consistencyValue,
                requestCharge: response.Headers.RequestCharge);

            return response;
        }

        /// <summary>
        /// It returns the payload size after reading it from the Response content stream. 
        /// To avoid blocking IO calls to get the stream length, it will return response content length if stream is of Memory Type
        /// otherwise it will return the content length from the response header (if it is there)
        /// </summary>
        /// <param name="response"></param>
        /// <returns>Size of Payload</returns>
        private static long GetPayloadSize(ResponseMessage response)
        {
            if (response != null)
            {
                if (response.Content != null && response.Content is MemoryStream)
                {
                    return response.Content.Length;
                }

                if (response.Headers != null && response.Headers.ContentLength != null)
                {
                    return long.Parse(response.Headers.ContentLength);
                }
            }

            return 0;
        }

        private static long GetPayloadSize<T>(Response<T> response)
        {
            if (response != null)
            {
                if (response.Headers != null && response.Headers.ContentLength != null)
                {
                    return long.Parse(response.Headers.ContentLength);
                }
            }

            return 0;
        }
    }
}
