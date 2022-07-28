//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using Diagnostics;
    using global::Azure.Core.Pipeline;

    internal readonly struct OpenTelemetryCoreRecorder : IDisposable
    {
        private const string CosmosDb = "cosmosdb";

        private readonly DiagnosticScope scope;
        private readonly OpenTelemetryOptions config;

        internal static IDictionary<Type, Action<Exception, DiagnosticScope>> OTelCompatibleExceptions = new Dictionary<Type, Action<Exception, DiagnosticScope>>()
        {
            { typeof(CosmosNullReferenceException), (exception, scope) => CosmosNullReferenceException.RecordOtelAttributes((CosmosNullReferenceException)exception, scope)},
            { typeof(CosmosObjectDisposedException), (exception, scope) => CosmosObjectDisposedException.RecordOtelAttributes((CosmosObjectDisposedException)exception, scope)},
            { typeof(CosmosOperationCanceledException), (exception, scope) => CosmosOperationCanceledException.RecordOtelAttributes((CosmosOperationCanceledException)exception, scope)},
            { typeof(CosmosException), (exception, scope) => CosmosException.RecordOtelAttributes((CosmosException)exception, scope)},
            { typeof(ChangeFeedProcessorUserException), (exception, scope) => ChangeFeedProcessorUserException.RecordOtelAttributes((ChangeFeedProcessorUserException)exception, scope)}
        };

        public OpenTelemetryCoreRecorder(DiagnosticScope scope, OpenTelemetryOptions config)
        {
            this.scope = scope;
            this.config = config ?? new OpenTelemetryOptions(); //If null load with default values

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
        /// System Level and Client level attributes
        /// </summary>
        /// <param name="operationName"></param>
        /// <param name="clientContext"></param>
        public void Record(string operationName, CosmosClientContext clientContext)
        {
            // Other information
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.DbSystemName, OpenTelemetryCoreRecorder.CosmosDb);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.DbOperation, operationName);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.MachineId, VmMetadataApiHandler.GetMachineId());
            
            string netPeerName = clientContext.DocumentClient?.accountServiceConfiguration?.AccountProperties?.AccountNameWithCloudInformation;
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.NetPeerName, netPeerName);

            // Client Information
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ClientId, clientContext.Client.Id);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.UserAgent, clientContext.UserAgent);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ConnectionMode, clientContext.ClientOptions.ConnectionMode);
        }

        /// <summary>
        /// Record attributes from response
        /// </summary>
        /// <param name="response"></param>
        public void Record(OpenTelemetryAttributes response)
        {
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.DbName, response.DatabaseName);

            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ContainerName, response.ContainerName);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestContentLength, response.RequestContentLength);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ResponseContentLength, response.ResponseContentLength);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.StatusCode, response.StatusCode);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestCharge, response.RequestCharge);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.ItemCount, response.ItemCount);
            this.scope.AddAttribute(OpenTelemetryAttributeKeys.Region, ClientTelemetryHelper.GetContactedRegions(response.Diagnostics));

            if (this.IsEnabled && DiagnosticsFilterHelper.IsAllowed(
                    config: this.config,
                    latency: response.Diagnostics.GetClientElapsedTime(),
                    statusCode: response.StatusCode))
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
