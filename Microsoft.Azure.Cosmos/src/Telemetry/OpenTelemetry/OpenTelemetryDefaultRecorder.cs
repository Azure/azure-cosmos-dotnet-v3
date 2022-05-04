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

    internal sealed class OpenTelemetryDefaultRecorder : IOpenTelemetryRecorder
    {
        private readonly DiagnosticScope scope;

        private HttpStatusCode statusCode;
        private double requestCharge;

        public OpenTelemetryDefaultRecorder(DiagnosticScope scope)
        {
            this.scope = scope;

            this.scope.Start();
        }

        public override void Record(string attributeKey, object attributeValue)
        {
            if (this.scope.IsEnabled)
            {
                if (attributeKey.Equals(OpenTelemetryAttributeKeys.RequestCharge, StringComparison.OrdinalIgnoreCase))
                {
                    this.requestCharge = Convert.ToDouble(attributeValue);
                }
                if (attributeKey.Equals(OpenTelemetryAttributeKeys.StatusCode, StringComparison.OrdinalIgnoreCase))
                {
                    this.statusCode = (HttpStatusCode)attributeValue;
                }
                this.scope.AddAttribute(attributeKey, attributeValue);
            }
        }

        public override void Record(ITrace trace)
        {
            if (this.scope.IsEnabled)
            {
                CosmosTraceDiagnostics diagnostics = new CosmosTraceDiagnostics(trace);
                this.Record(OpenTelemetryAttributeKeys.Region, ClientTelemetryHelper.GetContactedRegions(diagnostics));

                if (DiagnosticsFilterHelper.IsAllowed(
                        latency: diagnostics.GetClientElapsedTime(), 
                        statuscode: this.statusCode))
                {
                    this.Record(OpenTelemetryAttributeKeys.RequestDiagnostics, diagnostics.ToString());
                } 
            }
        }

        public override void MarkFailed(Exception exception)
        {
            if (this.scope.IsEnabled)
            {
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionMessage, exception.Message);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionStacktrace, exception.StackTrace);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionType, exception.GetType());

                this.scope.Failed(exception);
            }
        }

        public override void Dispose()
        {
            this.scope.Dispose();
        }
    }
}
