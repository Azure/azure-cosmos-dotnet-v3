//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Telemetry;

    internal class TelemetryHandler : RequestHandler
    {
        private readonly ClientTelemetry telemetry;

        public TelemetryHandler(ClientTelemetry telemetry)
        {
            Console.WriteLine("TelemetryHandler 1: " + GC.GetTotalMemory(true));
            this.telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        }

        public override async Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            Console.WriteLine("TelemetryHandler 2: " + GC.GetTotalMemory(true));
            ResponseMessage response = await base.SendAsync(request, cancellationToken);
            if (request.IsTelemetryAllowed())
            {
                Console.WriteLine("TelemetryHandler 3: " + GC.GetTotalMemory(true));
                try
                {
                    this.telemetry
                        .Collect(
                                cosmosDiagnostics: response.Diagnostics,
                                statusCode: response.StatusCode,
                                responseSizeInBytes: TelemetryHandler.GetPayloadSize(response),
                                containerId: request.ContainerId,
                                databaseId: request.DatabaseId,
                                operationType: request.OperationType,
                                resourceType: request.ResourceType,
                                consistencyLevel: request.Headers[Documents.HttpConstants.HttpHeaders.ConsistencyLevel],
                                requestCharge: response.Headers.RequestCharge);

                    Console.WriteLine("TelemetryHandler 4: " + GC.GetTotalMemory(true));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("TelemetryHandler 5: " + GC.GetTotalMemory(true));
                    DefaultTrace.TraceError("Error while collecting telemetry information : " + ex.Message);
                }
            }

            Console.WriteLine("TelemetryHandler 6: " + GC.GetTotalMemory(true));
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
    }
}
