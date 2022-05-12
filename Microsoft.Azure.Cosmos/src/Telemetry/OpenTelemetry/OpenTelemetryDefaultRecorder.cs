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

        private OpenTelemetryResponse response;

        public OpenTelemetryDefaultRecorder(DiagnosticScope scope)
        {
            this.scope = scope;
            this.scope.Start();
        }

        public override bool IsEnabled => this.scope.IsEnabled;

        public override void Record(string key, string value)
        {
            if (this.scope.IsEnabled)
            {
                this.scope.AddAttribute(key, value);
            }
        }

        public override void Record(OpenTelemetryResponse response)
        {
            if (this.scope.IsEnabled)
            {
                this.response = response;  

                this.scope.AddAttribute(OpenTelemetryAttributeKeys.StatusCode, response.StatusCode);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestContentLength, response.RequestContentLength);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.ResponseContentLength, response.ResponseContentLength);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.StatusCode, response.StatusCode);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestCharge, response.RequestCharge);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.ItemCount, response.ItemCount);
            }
        }

        public override void Record(ITrace trace)
        {
            if (this.scope.IsEnabled)
            {
                CosmosTraceDiagnostics diagnostics = new CosmosTraceDiagnostics(trace);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.Region, ClientTelemetryHelper.GetContactedRegions(diagnostics));

                if (DiagnosticsFilterHelper.IsAllowed(
                        latency: diagnostics.GetClientElapsedTime(), 
                        statuscode: this.response.StatusCode))
                {
                    this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestDiagnostics, diagnostics.ToString());
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
