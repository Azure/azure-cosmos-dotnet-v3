//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Telemetry.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// This class is used to add information in an Activity tags for OpenTelemetry.
    /// Refer to <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/3058"/> for more details.
    /// </summary>
    internal struct OpenTelemetryCoreRecorder : IDisposable
    {
        internal const string CosmosDb = "cosmosdb";

        private readonly DiagnosticScope scope = default;
        private readonly CosmosThresholdOptions config = null;
        private readonly Activity activity = null;

        private readonly OperationType operationType = OperationType.Invalid;
        private readonly string connectionModeCache = null;

        private readonly QueryTextMode? queryTextMode = null;

        private readonly IActivityAttributePopulator activityAttributePopulator = TracesStabilityFactory.GetAttributePopulator();

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
            this.connectionModeCache = clientContext.ClientOptions.ConnectionMode switch
            {
                ConnectionMode.Direct => "direct",
                ConnectionMode.Gateway => "gateway",
                _ => throw new NotImplementedException()
            };

            this.queryTextMode = queryTextMode;

            if (scope.IsEnabled)
            {
                this.scope.Start();

                this.scope.AddAttribute(OpenTelemetryAttributeKeys.DbSystemName, OpenTelemetryCoreRecorder.CosmosDb);

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
                this.activityAttributePopulator.PopulateAttributes(this.scope,
                    operationName,
                    databaseName,
                    containerName,
                    clientContext.Client?.Endpoint,
                    clientContext.UserAgent,
                    VmMetadataApiHandler.GetMachineId(),
                    clientContext?.Client?.Id,
                    this.connectionModeCache);
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
                this.activityAttributePopulator.PopulateAttributes(this.scope, exception);

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
                string operationTypeName = Enum.GetName(typeof(OperationType), operationType);

                if (this.response != null)
                {
                    this.activityAttributePopulator.PopulateAttributes(this.scope, this.queryTextMode, operationTypeName, this.response);

                    CosmosDbEventSource.RecordDiagnosticsForRequests(this.config, operationType, this.response);

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
