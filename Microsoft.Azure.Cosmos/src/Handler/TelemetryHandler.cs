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
    using Microsoft.Azure.Cosmos.Telemetry.Collector;

    internal class TelemetryHandler : RequestHandler
    {
        private readonly TelemetryToServiceHelper telemetryToServiceHelper;

        public TelemetryHandler(TelemetryToServiceHelper telemetryToServiceHelper)
        {
            this.telemetryToServiceHelper = telemetryToServiceHelper ?? throw new ArgumentNullException(nameof(telemetryToServiceHelper));
        }

        public override async Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await base.SendAsync(request, cancellationToken);
            if (this.IsAllowed(request))
            {
                try
                {
                    this.telemetryToServiceHelper.GetCollector().CollectOperationAndNetworkInfo(
                        () => new TelemetryInformation
                        {
                            RegionsContactedList = response.Diagnostics.GetContactedRegions(),
                            RequestLatency = response.Diagnostics.GetClientElapsedTime(),
                            StatusCode = response.StatusCode,
                            ResponseSizeInBytes = TelemetryHandler.GetPayloadSize(response),
                            ContainerId = request.ContainerId,
                            DatabaseId = request.DatabaseId,
                            OperationType = request.OperationType,
                            ResourceType = request.ResourceType,
                            ConsistencyLevel = request.Headers?[Documents.HttpConstants.HttpHeaders.ConsistencyLevel],
                            RequestCharge = response.Headers.RequestCharge,
                            SubStatusCode = response.Headers.SubStatusCode,
                            Trace = response.Trace,
                            TraceToLog = request.Trace
                        });
                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceError("Error while collecting telemetry information : {0}", ex);
                }
            }

            if (this.telemetryToServiceHelper.GetJobException() != null)
            {
                request.Trace.AddDatum(ClientTelemetryOptions.TelemetryToServiceJobException, this.telemetryToServiceHelper.GetJobException()?.ToString());
            }

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
