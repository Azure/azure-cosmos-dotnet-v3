//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Net;
    using global::Azure.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;

    internal class CosmosInstrumentation : ICosmosInstrumentation
    {
        private readonly DiagnosticScope scope;

        private HttpStatusCode statusCode;
        private double requestCharge;

        public CosmosInstrumentation(DiagnosticScope scope)
        {
            this.scope = scope;

            this.scope.Start();
        }

        public void Record(string attributeKey, object attributeValue)
        {
            if (this.scope.IsEnabled)
            {
                if (attributeKey.Equals(OTelAttributes.RequestCharge))
                {
                    this.requestCharge = Convert.ToDouble(attributeValue);
                }
                if (attributeKey.Equals(OTelAttributes.StatusCode))
                {
                    this.statusCode = (HttpStatusCode)attributeValue;
                }
                this.scope.AddAttribute(attributeKey, attributeValue);
            }
        }

        public void Record(ITrace trace)
        {
            if (this.scope.IsEnabled)
            {
                CosmosTraceDiagnostics diagnostics = new CosmosTraceDiagnostics(trace);

                this.Record(OTelAttributes.Region, ClientTelemetryHelper.GetContactedRegions(diagnostics));

                if (DiagnosticsFilterHelper.IsAllowed(
                        latency: diagnostics.GetClientElapsedTime(), 
                        requestcharge: this.requestCharge, 
                        statuscode: this.statusCode))
                {
                    this.Record(OTelAttributes.RequestDiagnostics, diagnostics.ToString());
                } 
            }
        }

        public void MarkFailed(Exception exception)
        {
            if (this.scope.IsEnabled)
            {
                this.scope.AddAttribute(OTelAttributes.ExceptionMessage, exception.Message);
                this.scope.AddAttribute(OTelAttributes.ExceptionStacktrace, exception.StackTrace);
                this.scope.AddAttribute(OTelAttributes.ExceptionType, exception.GetType());

                this.scope.Failed(exception);
            }
        }

        public void Dispose()
        {
            this.scope.Dispose();
        }
    }
}
