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

    internal sealed class DefaultRecorder : IRecorder
    {
        private readonly DiagnosticScope scope;

        private HttpStatusCode statusCode;
        private double requestCharge;

        public DefaultRecorder(DiagnosticScope scope)
        {
            this.scope = scope;

            this.scope.Start();
        }

        public override void Record(string attributeKey, object attributeValue)
        {
            if (this.scope.IsEnabled)
            {
                if (attributeKey.Equals(Attributes.RequestCharge, StringComparison.OrdinalIgnoreCase))
                {
                    this.requestCharge = Convert.ToDouble(attributeValue);
                }
                if (attributeKey.Equals(Attributes.StatusCode, StringComparison.OrdinalIgnoreCase))
                {
                    this.statusCode = (HttpStatusCode)attributeValue;
                }
                this.scope.AddAttribute(attributeKey, attributeValue);
            }
        }

        public override void Record(CosmosDiagnostics diagnostics)
        {
            if (this.scope.IsEnabled)
            {
                this.Record(Attributes.Region, ClientTelemetryHelper.GetContactedRegions(diagnostics));

                if (DiagnosticsFilterHelper.IsAllowed(
                        latency: diagnostics.GetClientElapsedTime(), 
                        statuscode: this.statusCode))
                {
                    this.Record(Attributes.RequestDiagnostics, diagnostics.ToString());
                } 
            }
        }

        public override void MarkFailed(Exception exception)
        {
            if (this.scope.IsEnabled)
            {
                this.scope.AddAttribute(Attributes.ExceptionMessage, exception.Message);
                this.scope.AddAttribute(Attributes.ExceptionStacktrace, exception.StackTrace);
                this.scope.AddAttribute(Attributes.ExceptionType, exception.GetType());

                this.scope.Failed(exception);
            }
        }

        public override void Dispose()
        {
            this.scope.Dispose();
        }
    }
}
