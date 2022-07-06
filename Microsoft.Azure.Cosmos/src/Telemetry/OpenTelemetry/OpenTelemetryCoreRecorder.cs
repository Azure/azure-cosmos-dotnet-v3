﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Net;
    using global::Azure.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;
    using Microsoft.Azure.Cosmos.Tracing;

    internal struct OpenTelemetryCoreRecorder : IDisposable
    {
        private readonly DiagnosticScope scope;

        public OpenTelemetryCoreRecorder(DiagnosticScope scope)
        {
            this.scope = scope;

            if (this.scope.IsEnabled)
            {
                this.scope.Start();
            }
        }

        public bool IsEnabled => this.scope.IsEnabled;

        public void Record(string key, string value)
        {
            this.scope.AddAttribute(key, value);
        }

        public void Record(OpenTelemetryAttributes response)
        {
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.StatusCode, response.StatusCode);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestContentLength, response.RequestContentLength);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ResponseContentLength, response.ResponseContentLength);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.StatusCode, response.StatusCode);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestCharge, response.RequestCharge);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ItemCount, response.ItemCount);

            this.scope.AddAttribute(OpenTelemetryAttributeKeys.Region, ClientTelemetryHelper.GetContactedRegions(response.Diagnostics));

            if (this.IsEnabled && DiagnosticsFilterHelper.IsAllowed(
                    latency: response.Diagnostics.GetClientElapsedTime(),
                    statuscode: response.StatusCode))
            {
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestDiagnostics, response.Diagnostics.ToString());
            }

        }

        public void MarkFailed(CosmosException exception)
        {
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.StatusCode, exception.StatusCode);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestCharge, exception.RequestCharge);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.Region, ClientTelemetryHelper.GetContactedRegions(exception.Diagnostics));

            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionMessage, exception.Message);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionStacktrace, exception.StackTrace);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionType, exception.GetType());

            this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestDiagnostics, exception.Diagnostics.ToString());

            this.scope.Failed(exception);
        }

        public void MarkFailed(CosmosNullReferenceException exception)
        {
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.Region, ClientTelemetryHelper.GetContactedRegions(exception.Diagnostics));

            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionMessage, exception.Message);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionStacktrace, exception.StackTrace);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionType, exception.GetType());

            this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestDiagnostics, exception.Diagnostics.ToString());

            this.scope.Failed(exception);
        }

        public void MarkFailed(CosmosObjectDisposedException exception)
        {
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.Region, ClientTelemetryHelper.GetContactedRegions(exception.Diagnostics));

            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionMessage, exception.Message);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionStacktrace, exception.StackTrace);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionType, exception.GetType());

            this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestDiagnostics, exception.Diagnostics.ToString());

            this.scope.Failed(exception);
        }

        public void MarkFailed(CosmosOperationCanceledException exception)
        {
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.Region, ClientTelemetryHelper.GetContactedRegions(exception.Diagnostics));

            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionMessage, exception.Message);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionStacktrace, exception.StackTrace);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionType, exception.GetType());

            this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestDiagnostics, exception.Diagnostics.ToString());

            this.scope.Failed(exception);
        }

        public void Dispose()
        {
            if (this.scope.IsEnabled)
            {
                this.scope.Dispose();
            }
        }
    }
}
