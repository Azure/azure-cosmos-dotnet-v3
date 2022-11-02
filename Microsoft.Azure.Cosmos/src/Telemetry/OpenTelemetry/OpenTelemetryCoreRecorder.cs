//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using Diagnostics;
    using global::Azure.Core.Pipeline;

    internal struct OpenTelemetryCoreRecorder : IDisposable
    {
        private const string CosmosDb = "cosmosdb";

        private readonly DiagnosticScope scope;
        private readonly DistributedTracingOptions config;

        internal static IDictionary<Type, Action<Exception, DiagnosticScope>> OTelCompatibleExceptions = new Dictionary<Type, Action<Exception, DiagnosticScope>>()
        {
            { typeof(CosmosNullReferenceException), (exception, scope) => CosmosNullReferenceException.RecordOtelAttributes((CosmosNullReferenceException)exception, scope)},
            { typeof(CosmosObjectDisposedException), (exception, scope) => CosmosObjectDisposedException.RecordOtelAttributes((CosmosObjectDisposedException)exception, scope)},
            { typeof(CosmosOperationCanceledException), (exception, scope) => CosmosOperationCanceledException.RecordOtelAttributes((CosmosOperationCanceledException)exception, scope)},
            { typeof(CosmosException), (exception, scope) => CosmosException.RecordOtelAttributes((CosmosException)exception, scope)},
            { typeof(ChangeFeedProcessorUserException), (exception, scope) => ChangeFeedProcessorUserException.RecordOtelAttributes((ChangeFeedProcessorUserException)exception, scope)}
        };

        public OpenTelemetryCoreRecorder(DiagnosticScope scope, CosmosClientContext clientContext, DistributedTracingOptions config)
        {
            this.scope = scope;
            this.config = config;

            if (this.IsEnabled)
            {
                this.scope.Start();

                this.Record(clientContext);
            }
        }

        public bool IsEnabled => this.scope.IsEnabled;

        public void Record(string key, string value)
        {
            if (this.IsEnabled)
            {
                this.scope.AddAttribute(key, value);
            }
        }

        /// <summary>
        /// System Level and Client level attributes
        /// </summary>
        /// <param name="clientContext"></param>
        public void Record(CosmosClientContext clientContext)
        {
            if (this.IsEnabled)
            {
                // Other information
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.DbSystemName, OpenTelemetryCoreRecorder.CosmosDb);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.MachineId, VmMetadataApiHandler.GetMachineId());
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.NetPeerName, clientContext.Client?.Endpoint?.Host);

                // Client Information
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.ClientId, clientContext?.Client?.Id ?? OpenTelemetryAttributes.NotAvailable);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.UserAgent, clientContext.UserAgent ?? OpenTelemetryAttributes.NotAvailable);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.ConnectionMode, clientContext.ClientOptions.ConnectionMode);
            }
        }

        /// <summary>
        /// Record attributes from response
        /// </summary>
        /// <param name="response"></param>
        public void Record(OpenTelemetryAttributes response)
        {
            if (this.IsEnabled)
            {
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.DbName, response.DatabaseName);

                this.scope.AddAttribute(OpenTelemetryAttributeKeys.ContainerName, response.ContainerName);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestContentLength, response.RequestContentLength);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.ResponseContentLength, response.ResponseContentLength);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.StatusCode, response.StatusCode);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.SubStatusCode, response.SubStatusCode);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestCharge, response.RequestCharge);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.ItemCount, response.ItemCount);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.OperationType, response.OperationType);

                if (response.Diagnostics != null)
                {
                    this.scope.AddAttribute(OpenTelemetryAttributeKeys.Region, ClientTelemetryHelper.GetContactedRegions(response.Diagnostics.GetContactedRegions()) ?? OpenTelemetryAttributes.NotAvailable);
                    CosmosDbEventSource.RecordDiagnosticsForRequests(this.config, response);
                }
                else
                {
                    this.scope.AddAttribute(OpenTelemetryAttributeKeys.Region, OpenTelemetryAttributes.NotAvailable);
                    this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestDiagnostics, OpenTelemetryAttributes.NotAvailable);
                }
            }
        }

        /// <summary>
        /// Record attributes during exception
        /// </summary>
        /// <param name="openTelemetryException"></param>
        public void MarkFailed(OpenTelemetryException openTelemetryException)
        {
            if (this.IsEnabled)
            {
                Exception exception = openTelemetryException.OriginalException;
                
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionStacktrace, exception.StackTrace);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionType, exception.GetType());

                // If Exception is not registered with open Telemetry
                if (!OpenTelemetryCoreRecorder.IsExceptionRegistered(exception, this.scope))
                {
                    this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionMessage, exception.Message);
                }

                this.scope.Failed(exception);
            }
        }

        /// <summary>
        /// Checking if passed exception is registered with Open telemetry or Not
        /// </summary>
        /// <param name="exception"></param>
        /// <param name="scope"></param>
        /// <returns>tru/false</returns>
        internal static bool IsExceptionRegistered(Exception exception, DiagnosticScope scope)
        {
            foreach (KeyValuePair<Type, Action<Exception, DiagnosticScope>> registeredExceptionHandlers in OpenTelemetryCoreRecorder.OTelCompatibleExceptions)
            {
                Type exceptionType = exception.GetType();
                if (registeredExceptionHandlers.Key.IsAssignableFrom(exceptionType))
                {
                    registeredExceptionHandlers.Value(exception, scope);

                    return true;
                }
            }

            return false;
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
