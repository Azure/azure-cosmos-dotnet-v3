//------------------------------------------------------------
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
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestDiagnostics, response.Diagnostics);
            }

        }

        public void MarkFailed<T>(T exception)
             where T : Exception
        {
            string exceptionMessage = null;

            if (exception is CosmosException cosmosException)
            {
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.StatusCode, cosmosException.StatusCode);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestCharge, cosmosException.RequestCharge);

                this.scope.AddAttribute(OpenTelemetryAttributeKeys.Region, ClientTelemetryHelper.GetContactedRegions(cosmosException.Diagnostics));
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestDiagnostics, cosmosException.Diagnostics);

                exceptionMessage = cosmosException.Message;
            }

            if (exception is CosmosNullReferenceException cosmosNullReferenceException)
            {
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.Region, ClientTelemetryHelper.GetContactedRegions(cosmosNullReferenceException.Diagnostics));
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestDiagnostics, cosmosNullReferenceException.Diagnostics);

                exceptionMessage = cosmosNullReferenceException.GetBaseException().Message;
            }

            if (exception is CosmosObjectDisposedException cosmosObjectDisposedException)
            {
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.Region, ClientTelemetryHelper.GetContactedRegions(cosmosObjectDisposedException.Diagnostics));
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestDiagnostics, cosmosObjectDisposedException.Diagnostics);

                exceptionMessage = cosmosObjectDisposedException.Message;
            }

            if (exception is CosmosOperationCanceledException cosmosOperationCanceledException)
            {
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.Region, ClientTelemetryHelper.GetContactedRegions(cosmosOperationCanceledException.Diagnostics));
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestDiagnostics, cosmosOperationCanceledException.Diagnostics);

                exceptionMessage = cosmosOperationCanceledException.GetBaseException().Message;
            }

            if (exceptionMessage == null)
            {
                exceptionMessage = exception.Message;
            }

            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionMessage, exceptionMessage);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionStacktrace, exception.StackTrace);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionType, exception.GetType());

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
