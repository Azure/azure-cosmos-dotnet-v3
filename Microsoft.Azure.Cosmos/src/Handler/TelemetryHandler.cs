//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Telemetry.Payloads;

    internal class TelemetryHandler : RequestHandler
    {
        private static readonly DiagnosticSource diagnosticSource = new DiagnosticListener(ClientTelemetryOptions.DiagnosticSourceName);

        public override async Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await base.SendAsync(request, cancellationToken);
            Stopwatch stopwatch = Stopwatch.StartNew();
            if (this.IsAllowed(request))
            {
                try
                {
                    //Console.WriteLine("Diagnostic Source ClientTelmetry ");

                    if (diagnosticSource.IsEnabled(ClientTelemetryOptions.DiagnosticSourceName))
                    {
                        RequestPayload payload = new RequestPayload(
                                id: Guid.NewGuid().ToString(),  
                                cosmosDiagnostics: response.Diagnostics,
                                statusCode: response.StatusCode,
                                responseSizeInBytes: this.GetPayloadSize(response),
                                containerId: request.ContainerId,
                                databaseId: request.DatabaseId,
                                operationType: request.OperationType,
                                resourceType: request.ResourceType,
                                consistencyLevel: request.Headers?[Documents.HttpConstants.HttpHeaders.ConsistencyLevel],
                                requestCharge: response.Headers.RequestCharge);

                        //Console.WriteLine("Diagnostic Source ClientTelmetry enabled, payload id " + payload.Id);
                        diagnosticSource.Write(ClientTelemetryOptions.RequestPayloadKey, payload);
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    DefaultTrace.TraceError("Error while collecting telemetry information : " + ex.Message);
                }
            }

            stopwatch.Stop();

            Console.WriteLine("time to collect " + stopwatch.Elapsed.TotalMilliseconds);
            return response;
        }

        private bool IsAllowed(RequestMessage request)
        { 
            return ClientTelemetryOptions.AllowedResourceTypes.Equals(request.ResourceType);
        }

        /// <summary>
        /// It returns the payload size after reading it from the Response content stream. 
        /// To avoid blocking IO calls to get the stream length, it will return response content length if stream is of Memory Type
        /// otherwise it will return the content length from the response header (if it is there)
        /// </summary>
        /// <param name="response"></param>
        /// <returns>Size of Payload</returns>
        private long GetPayloadSize(ResponseMessage response)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            if (response != null)
            {
                if (response.Content != null && response.Content is MemoryStream)
                {
                    long value = response.Content.Length;
                    stopwatch.Stop();

                    Console.WriteLine(" time to get response payloadsize " + stopwatch.Elapsed.TotalMilliseconds);
                    return value;
                }

                if (response.Headers != null && response.Headers.ContentLength != null)
                {
                    long value = long.Parse(response.Headers.ContentLength);

                    stopwatch.Stop();

                    Console.WriteLine(" time to get response payloadsize " + stopwatch.Elapsed.TotalMilliseconds);

                    return value;
                }
            }

            stopwatch.Stop();

            Console.WriteLine(" time to get response payloadsize " + stopwatch.Elapsed.TotalMilliseconds);
            return 0;
        }
    }
}
