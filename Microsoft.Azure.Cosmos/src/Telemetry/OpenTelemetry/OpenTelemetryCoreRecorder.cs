//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using global::Azure.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;

    internal struct OpenTelemetryCoreRecorder : IDisposable
    {
        private readonly DiagnosticScope scope;

        internal static IDictionary<Type, Action<Exception, DiagnosticScope>> oTelCompatibleExceptions = new Dictionary<Type, Action<Exception, DiagnosticScope>>()
        {
            { typeof(CosmosNullReferenceException), (exception, scope) => CosmosNullReferenceException.RecordOtelAttributes((CosmosNullReferenceException)exception, scope)},
            { typeof(CosmosObjectDisposedException), (exception, scope) => CosmosObjectDisposedException.RecordOtelAttributes((CosmosObjectDisposedException)exception, scope)},
            { typeof(CosmosOperationCanceledException), (exception, scope) => CosmosOperationCanceledException.RecordOtelAttributes((CosmosOperationCanceledException)exception, scope)},
            { typeof(CosmosException), (exception, scope) => CosmosException.RecordOtelAttributes((CosmosException)exception, scope)},
            { typeof(ChangeFeedProcessorUserException), (exception, scope) => ChangeFeedProcessorUserException.RecordOtelAttributes((ChangeFeedProcessorUserException)exception, scope)}
        };

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

        /// <summary>
        /// Record attributes from response
        /// </summary>
        /// <param name="response"></param>
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

        /// <summary>
        /// Record attributes during exception
        /// </summary>
        /// <param name="exception"></param>
        public void MarkFailed(Exception exception)
        {
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionStacktrace, exception.StackTrace);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionType, exception.GetType());

            // If Exception is not registered with open Telemetry
            if (!OpenTelemetryCoreRecorder.IsExceptionRegistered(exception, this.scope))
            {
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionMessage, exception.Message);
            }

            this.scope.Failed(exception);
        }

        /// <summary>
        /// Checking if passed exception is registered with Open telemetry or Not
        /// </summary>
        /// <param name="exception"></param>
        /// <param name="scope"></param>
        /// <returns>tru/false</returns>
        internal static bool IsExceptionRegistered(Exception exception, DiagnosticScope scope)
        {
            bool isExceptionRegistered = false;
            foreach (KeyValuePair<Type, Action<Exception, DiagnosticScope>> registeredExceptionHandlers in OpenTelemetryCoreRecorder.oTelCompatibleExceptions)
            {
                Type exceptionType = exception.GetType();
                if (registeredExceptionHandlers.Key.IsAssignableFrom(exceptionType))
                {
                    registeredExceptionHandlers.Value(exception, scope);

                    isExceptionRegistered = true;
                    break;
                }
            }

            return isExceptionRegistered;
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
