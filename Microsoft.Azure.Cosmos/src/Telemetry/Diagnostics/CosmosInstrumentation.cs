//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Net;
    using Documents;
    using global::Azure.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;

    internal class CosmosInstrumentation : ICosmosInstrumentation
    {
        private readonly DiagnosticScope scope;

        private DiagnosticAttributes Attributes { get; }

        public CosmosInstrumentation(DiagnosticScope scope)
        {
            this.Attributes = new DiagnosticAttributes();

            this.scope = scope;
            this.scope.Start();
        }

        public void MarkFailed(Exception ex)
        {
            this.Attributes.IsError = true;
            this.Attributes.ExceptionStackTrace = ex.StackTrace;
            
            this.scope.Failed(ex);
        }

        public void Record(double? requestCharge = null,
            string operationType = null,
            HttpStatusCode? statusCode = null, 
            string databaseId = null, 
            string containerId = null,
            string subStatusCode = null,
            int? itemCount = null,
            long? requestSize = null,
            long? responseSize = null,
            Uri accountName = null, 
            string userAgent = null, 
            ConnectionMode? connectionMode = null,
            Exception exception = null)
        {
            if (this.Attributes != null)
            {
                if (!string.IsNullOrEmpty(databaseId))
                {
                    this.Attributes.DbName = databaseId;
                }

                if (!string.IsNullOrEmpty(containerId))
                {
                    this.Attributes.ContainerName = containerId;
                }

                if (!string.IsNullOrEmpty(operationType))
                {
                    this.Attributes.DbOperation = operationType;
                }

                if (statusCode != null)
                {
                    this.Attributes.HttpStatusCode = statusCode;
                }

                if (subStatusCode != null)
                {
                    this.Attributes.BackendStatusCode = null;
                }

                if (itemCount.HasValue)
                {
                    this.Attributes.ItemCount = itemCount.Value;
                }

                if (requestCharge.HasValue)
                {
                    this.Attributes.RequestCharge = requestCharge.Value;
                }

                if (requestSize.HasValue)
                {
                    this.Attributes.RequestSize = requestSize.Value;
                }

                if (responseSize.HasValue)
                {
                    this.Attributes.ResponseSize = responseSize.Value;
                }
                
                if (accountName != null)
                {
                    this.Attributes.AccountName = accountName;
                }
                
                if (string.IsNullOrEmpty(userAgent))
                {
                    this.Attributes.UserAgent = userAgent;
                }
                
                if (!connectionMode.HasValue)
                {
                    this.Attributes.ConnectionMode = connectionMode.Value;
                }

                if (exception != null)
                {
                    this.Attributes.IsError = true;
                    this.Attributes.ExceptionStackTrace = exception.StackTrace;
                    this.Attributes.ExceptionType = exception.GetType().ToString();
                    this.Attributes.ExceptionMessage = exception.Message;
                }
                
            }
        }

        public void Record(ITrace trace)
        {
            if (this.Attributes != null)
            {
                this.Attributes.RequestDiagnostics = new CosmosTraceDiagnostics(trace);
            }
        }

        private void AddAttributesToScope()
        {
            if (this.Attributes == null)
            {
                return;
            }

            this.scope.AddAttribute(CosmosInstrumentationConstants.DbSystemName, this.Attributes.DbSystem);
            this.scope.AddAttribute(CosmosInstrumentationConstants.DbName, this.Attributes.DbName);
            this.scope.AddAttribute(CosmosInstrumentationConstants.DbOperation, this.Attributes.DbOperation);

            this.scope.AddAttribute(CosmosInstrumentationConstants.Account, this.Attributes.AccountName?.ToString());
            this.scope.AddAttribute(CosmosInstrumentationConstants.ContainerName, this.Attributes.ContainerName);

            this.scope.AddAttribute(CosmosInstrumentationConstants.PartitionId, "any value partitionid");

            if (this.Attributes.HttpStatusCode.HasValue)
            {
                this.scope.AddAttribute(CosmosInstrumentationConstants.StatusCode, (int)this.Attributes.HttpStatusCode.Value);
            }
            this.scope.AddAttribute(CosmosInstrumentationConstants.UserAgent, this.Attributes.UserAgent);
            this.scope.AddAttribute(CosmosInstrumentationConstants.RequestContentLength, "10");
            this.scope.AddAttribute(CosmosInstrumentationConstants.ResponseContentLength, "20");
            this.scope.AddAttribute(CosmosInstrumentationConstants.Region, "region");
            this.scope.AddAttribute(CosmosInstrumentationConstants.RetryCount, "2");
            this.scope.AddAttribute(CosmosInstrumentationConstants.ConnectionMode, this.Attributes.ConnectionMode);
            this.scope.AddAttribute(CosmosInstrumentationConstants.BackendStatusCodes, this.Attributes.BackendStatusCode);
            this.scope.AddAttribute(CosmosInstrumentationConstants.ItemCount, this.Attributes.ItemCount);

            if (this.Attributes.RequestCharge.HasValue)
            {
                this.scope.AddAttribute(CosmosInstrumentationConstants.RequestCharge, this.Attributes.RequestCharge.Value);

            }

            if (this.Attributes.IsError)
            {
                this.scope.AddAttribute(CosmosInstrumentationConstants.ExceptionStacktrace, this.Attributes.ExceptionStackTrace);
                this.scope.AddAttribute(CosmosInstrumentationConstants.ExceptionMessage, this.Attributes.ExceptionMessage);
                this.scope.AddAttribute(CosmosInstrumentationConstants.ExceptionType, this.Attributes.ExceptionType);
            }

            IDiagnosticsFilter filter = new DiagnosticsFilter(this.Attributes);
            if (this.Attributes.IsError || filter.IsAllowed())
            {
                this.scope.AddAttribute(CosmosInstrumentationConstants.RequestDiagnostics, this.Attributes.RequestDiagnostics);
            }
        }

        public void Dispose()
        {
            this.AddAttributesToScope();
            this.scope.Dispose();
        }
    }
}
