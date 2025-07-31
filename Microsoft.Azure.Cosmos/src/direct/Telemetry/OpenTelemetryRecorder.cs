//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if NETSTANDARD2_0_OR_GREATER
namespace Microsoft.Azure.Documents.Telemetry
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// This class is used to add information in an Activity tags
    /// </summary>
    internal class OpenTelemetryRecorder : IDisposable
    {
        private const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ssZZ";
        private readonly DiagnosticScope scope;
        private DistributedTracingOptions options;
        private DocumentServiceRequest request;

        public OpenTelemetryRecorder(DiagnosticScope scope, DocumentServiceRequest request, DistributedTracingOptions options)
        {
            this.request = request;
            this.options = options;
            this.scope = scope;
            this.scope.Start();
        }

        public void Record(Uri addressUri, 
                           Exception exception = null,
                           StoreResponse storeResponse = null)
        {
#pragma warning disable CDX1003 // Experimental - DontCatchGenericExceptions
            try
            {
                this.scope.AddAttribute("rntbd.url", addressUri.OriginalString);
                if (exception == null)
                {
                    //record activity
                    this.scope.AddIntegerAttribute("rntbd.sub_status_code", 0);
                    this.scope.AddIntegerAttribute("rntbd.status_code", (int)storeResponse.StatusCode);
                }
                else
                {   
                    if(exception is DocumentClientException docException)
                    {
                        this.scope.AddIntegerAttribute("rntbd.status_code", (int)docException.StatusCode);
                        this.scope.AddIntegerAttribute("rntbd.sub_status_code", (int)docException.GetSubStatus());
                    }
                    this.scope.AddAttribute("exception.type", exception.GetType().FullName);
                    this.scope.AddAttribute("exception.timestamp", DateTimeOffset.Now.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
                    this.scope.AddAttribute("exception.message", exception.Message);

                    this.scope.Failed(exception);

                }
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning("Error with distributed tracing {0}", ex.ToString());
            }
        }
        public void Dispose()
        {
            try
            {
                this.scope.Dispose();
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning("Error with diagnostic scope dispose {0}", ex.ToString());
#pragma warning restore CDX1003
            }
        }
    }
}
#endif