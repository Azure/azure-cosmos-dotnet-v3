//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Antlr4.Runtime.Misc;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Telemetry.Diagnostics;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// This class is used to add information in an Activity tags for OpenTelemetry.
    /// Refer to <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/3058"/> for more details.
    /// </summary>
    internal struct OpenTelemetryCoreRecorder : IDisposable
    {
        private const string CosmosDb = "cosmosdb";

        private static readonly string otelStabilityMode = Environment.GetEnvironmentVariable("OTEL_SEMCONV_STABILITY_OPT_IN");

        private readonly DiagnosticScope scope = default;
        private readonly CosmosThresholdOptions config = null;
        private readonly Activity activity = null;

        private readonly OperationType operationType = OperationType.Invalid;
        private readonly string connectionModeCache = null;

        private readonly QueryTextMode? queryTextMode = null;
        private OpenTelemetryAttributes response = null;

        /// <summary>
        /// Maps exception types to actions that record their OpenTelemetry attributes.
        /// </summary>
        internal static IDictionary<Type, Action<Exception, DiagnosticScope>> OTelCompatibleExceptions = new Dictionary<Type, Action<Exception, DiagnosticScope>>()
        {
            { typeof(CosmosNullReferenceException), (exception, scope) => CosmosNullReferenceException.RecordOtelAttributes((CosmosNullReferenceException)exception, scope)},
            { typeof(CosmosObjectDisposedException), (exception, scope) => CosmosObjectDisposedException.RecordOtelAttributes((CosmosObjectDisposedException)exception, scope)},
            { typeof(CosmosOperationCanceledException), (exception, scope) => CosmosOperationCanceledException.RecordOtelAttributes((CosmosOperationCanceledException)exception, scope)},
            { typeof(CosmosException), (exception, scope) => CosmosException.RecordOtelAttributes((CosmosException)exception, scope)},
            { typeof(ChangeFeedProcessorUserException), (exception, scope) => ChangeFeedProcessorUserException.RecordOtelAttributes((ChangeFeedProcessorUserException)exception, scope)}
        };

        private OpenTelemetryCoreRecorder(DiagnosticScope scope)
        {
            this.scope = scope;
            this.scope.Start();
        }

        private OpenTelemetryCoreRecorder(string operationName)
        {
            this.activity = new Activity(operationName);
            this.activity.Start();
        }

        private OpenTelemetryCoreRecorder(
            DiagnosticScope scope,
            string operationName,
            string containerName,
            string databaseName,
            OperationType operationType, 
            CosmosClientContext clientContext, 
            CosmosThresholdOptions config,
            QueryTextMode queryTextMode)
        {
            this.scope = scope;
            this.config = config;

            this.operationType = operationType;
            this.connectionModeCache = Enum.GetName(typeof(ConnectionMode), clientContext.ClientOptions.ConnectionMode);
            this.queryTextMode = queryTextMode;

            if (scope.IsEnabled)
            {
                this.scope.Start();

                this.Record(
                        operationName: operationName,
                        containerName: containerName,
                        databaseName: databaseName,
                        clientContext: clientContext);
            }
        }

        /// <summary>
        /// Creates a parent activity for scenarios where there are no listeners at the operation level but are present at the network level.
        /// </summary>
        /// <param name="networkScope">The network-level diagnostic scope.</param>
        /// <returns>An instance of <see cref="OpenTelemetryCoreRecorder"/>.</returns>
        public static OpenTelemetryCoreRecorder CreateNetworkLevelParentActivity(DiagnosticScope networkScope)
        {
            return new OpenTelemetryCoreRecorder(networkScope);
        }

        /// <summary>
        /// Used for creating parent activity in scenario where there are no listeners at operation level and network level
        /// </summary>
        public static OpenTelemetryCoreRecorder CreateParentActivity(string operationName)
        {
            return new OpenTelemetryCoreRecorder(operationName);
        }

        /// <summary>
        /// Used for creating parent activity in scenario where there are listeners at operation level 
        /// </summary>
        public static OpenTelemetryCoreRecorder CreateOperationLevelParentActivity(
            DiagnosticScope operationScope,
            string operationName,
            string containerName,
            string databaseName,
            Documents.OperationType operationType,
            CosmosClientContext clientContext,
            CosmosThresholdOptions config, 
            QueryTextMode queryTextMode)
        {
            return new OpenTelemetryCoreRecorder(
                        operationScope,
                        operationName,
                        containerName,
                        databaseName,
                        operationType,
                        clientContext,
                        config,
                        queryTextMode);
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
        /// Recording information
        /// </summary>
        /// <param name="operationName"></param>
        /// <param name="containerName"></param>
        /// <param name="databaseName"></param>
        /// <param name="clientContext"></param>
        public void Record(
            string operationName,
            string containerName,
            string databaseName,
            CosmosClientContext clientContext)
        {
            if (this.IsEnabled)
            {
                if (otelStabilityMode == OpenTelemetryStablityModes.DatabaseDupe)
                {
                    this.scope.AddAttribute(OpenTelemetryAttributeKeys.DbOperation, operationName);
                    this.scope.AddAttribute(OpenTelemetryAttributeKeys.DbName, databaseName);
                    this.scope.AddAttribute(OpenTelemetryAttributeKeys.ContainerName, containerName);
                    this.scope.AddAttribute(OpenTelemetryAttributeKeys.ServerAddress, clientContext.Client?.Endpoint?.Host);
                    this.scope.AddAttribute(OpenTelemetryAttributeKeys.UserAgent, clientContext.UserAgent);
                   
                }
                else
                {
                    // Classic Appinsights Support
                    this.scope.AddAttribute(AppInsightClassicAttributeKeys.DbOperation, operationName);
                    this.scope.AddAttribute(AppInsightClassicAttributeKeys.DbName, databaseName);
                    this.scope.AddAttribute(AppInsightClassicAttributeKeys.ContainerName, containerName);
                    this.scope.AddAttribute(AppInsightClassicAttributeKeys.ServerAddress, clientContext.Client?.Endpoint?.Host);
                    this.scope.AddAttribute(AppInsightClassicAttributeKeys.UserAgent, clientContext.UserAgent);
                }

                // Other information
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.DbSystemName, OpenTelemetryCoreRecorder.CosmosDb);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.MachineId, VmMetadataApiHandler.GetMachineId());

                // Client Information
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.ClientId, clientContext?.Client?.Id);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.ConnectionMode, this.connectionModeCache);

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
                this.response = response;
            }
        }

        /// <summary>
        /// Record attributes during exception
        /// </summary>
        /// <param name="exception"></param>
        public void MarkFailed(Exception exception)
        {
            if (this.IsEnabled)
            {
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionStacktrace, exception.StackTrace);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionType, exception.GetType().Name);

                // If Exception is not registered with open Telemetry
                if (!OpenTelemetryCoreRecorder.IsExceptionRegistered(exception, this.scope))
                {
                    this.scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionMessage, exception.Message);
                }

                if (exception is not CosmosException || (exception is CosmosException cosmosException
                            && !DiagnosticsFilterHelper
                                    .IsSuccessfulResponse(cosmosException.StatusCode, cosmosException.SubStatusCode)))
                {
                    this.scope.Failed(exception);
                }
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
            if (this.IsEnabled)
            {
                OperationType operationType
                    = (this.response == null || this.response?.OperationType == OperationType.Invalid) ? this.operationType : this.response.OperationType;

                string operationName = Enum.GetName(typeof(OperationType), operationType);
                this.scope.AddAttribute(OpenTelemetryAttributeKeys.OperationType, operationName);

                if (this.response != null)
                {
                    if (this.response.BatchSize is not null)
                    {
                        this.scope.AddIntegerAttribute(OpenTelemetryAttributeKeys.BatchSize, (int)this.response.BatchSize);
                    }
                    this.scope.AddAttribute(OpenTelemetryAttributeKeys.RequestContentLength, this.response.RequestContentLength);
                    this.scope.AddAttribute(OpenTelemetryAttributeKeys.ResponseContentLength, this.response.ResponseContentLength);

                    if (otelStabilityMode == OpenTelemetryStablityModes.DatabaseDupe)
                    {
                        this.scope.AddIntegerAttribute(OpenTelemetryAttributeKeys.StatusCode, (int)this.response.StatusCode);
                    }
                    else
                    {
                        this.scope.AddIntegerAttribute(AppInsightClassicAttributeKeys.StatusCode, (int)this.response.StatusCode);
                    }

                    this.scope.AddIntegerAttribute(OpenTelemetryAttributeKeys.SubStatusCode, this.response.SubStatusCode);
                    this.scope.AddIntegerAttribute(OpenTelemetryAttributeKeys.RequestCharge, (int)this.response.RequestCharge);
                    this.scope.AddAttribute(OpenTelemetryAttributeKeys.ItemCount, this.response.ItemCount);
                    this.scope.AddAttribute(OpenTelemetryAttributeKeys.ActivityId, this.response.ActivityId);
                    this.scope.AddAttribute(OpenTelemetryAttributeKeys.CorrelatedActivityId, this.response.CorrelatedActivityId);

                    if (this.response.QuerySpec is not null)
                    {
                        if (this.queryTextMode == QueryTextMode.All || 
                            (this.queryTextMode == QueryTextMode.ParameterizedOnly && this.response.QuerySpec.ShouldSerializeParameters()))
                        {
                            this.scope.AddAttribute(OpenTelemetryAttributeKeys.QueryText, this.response.QuerySpec?.QueryText);
                        }
                    }
                    
                    if (this.response.Diagnostics != null)
                    {
                        this.scope.AddAttribute(OpenTelemetryAttributeKeys.Region, ClientTelemetryHelper.GetContactedRegions(this.response.Diagnostics.GetContactedRegions()));
                        CosmosDbEventSource.RecordDiagnosticsForRequests(this.config, operationType, this.response);
                    }

                    if (!DiagnosticsFilterHelper.IsSuccessfulResponse(this.response.StatusCode, this.response.SubStatusCode))
                    {
                        this.scope.Failed($"{(int)this.response.StatusCode}/{this.response.SubStatusCode}");
                    }
                }

                this.scope.Dispose();
            }
            else
            {
                this.activity?.Stop();
            }
        }
    }
}
