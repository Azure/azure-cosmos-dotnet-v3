//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if NETSTANDARD2_0_OR_GREATER
namespace Microsoft.Azure.Documents.Telemetry
{
    using System;
    using System.Globalization;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// This class is used to add information in an Activity tags
    /// </summary>
    internal class OpenTelemetryRecorder : IDisposable
    {
        private const string CosmosDb = "cosmosdb";
        private const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ssZZ";
        private readonly DiagnosticScope scope;

        public OpenTelemetryRecorder(DiagnosticScope scope)
        {
            this.scope = scope;
            this.scope.Start();
        }

        public void Record(Uri addressUri, 
                           Exception exception = null, 
                           DocumentClientException documentClientException = null, 
                           StoreResponse storeResponse = null)
        {
            try
            {
                this.scope.AddAttribute("rntbd.uri", addressUri);

                if (exception == null && documentClientException == null)
                {
                    //record activity
                    this.scope.AddAttribute("rntbd.sub_status_code", storeResponse.SubStatusCode);
                    this.scope.AddAttribute("rntbd.status_code", storeResponse.StatusCode);
                }
                else
                {   
                    //record exception
                    if (exception != null)
                    {
                        this.scope.AddAttribute("exception.type", exception.GetType().FullName);
                        this.scope.AddAttribute("exception.timestamp", DateTimeOffset.Now.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
                        this.scope.AddAttribute("exception.message", exception.Message);
                    }
                    else if (documentClientException != null)
                    {
                        this.scope.AddAttribute("rntbd.status_code", documentClientException.StatusCode);
                        this.scope.AddAttribute("rntbd.sub_status_code", documentClientException.GetSubStatus());
                        this.scope.AddAttribute("exception.type", documentClientException.GetType().FullName);
                        this.scope.AddAttribute("exception.timestamp", DateTimeOffset.Now.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
                        this.scope.AddAttribute("exception.message", documentClientException.Message);
                    }
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
            }
        }
    }
}
#endif