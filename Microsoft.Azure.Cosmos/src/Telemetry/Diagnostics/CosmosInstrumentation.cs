//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Net;
    using Documents;
    using global::Azure.Core.Pipeline;

    internal class CosmosInstrumentation : ICosmosInstrumentation
    {
        private readonly DiagnosticScope scope;

        private DiagnosticAttributes Attributes { get; }

        public CosmosInstrumentation(DiagnosticScope scope)
        {
            this.scope = scope;
            this.Attributes = new DiagnosticAttributes();

            this.scope.Start();
        }

        public void MarkFailed(Exception ex)
        {
            this.Attributes.Error = true;
            this.Attributes.ExceptionStackTrace = ex.StackTrace;
            
            this.scope.Failed(ex);
        }

        public void Record(double requestCharge,
            OperationType operationType,
            HttpStatusCode statusCode, 
            string databaseId = null, 
            string containerId = null,
            string queryText = null)
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

                if (!string.IsNullOrEmpty(queryText))
                {
                    this.Attributes.QueryText = queryText;
                }
                this.Attributes.DbOperation = operationType;
                this.Attributes.HttpStatusCode = statusCode;
                this.Attributes.RequestCharge = requestCharge;
            }
        }
        
        public void Record(Uri accountName, string userAgent, ConnectionMode connectionMode)
        {
            if (this.Attributes != null)
            {
                this.Attributes.AccountName = accountName;
                this.Attributes.UserAgent = userAgent;
                this.Attributes.ConnectionMode = connectionMode;
            }
        }

        public void Record(CosmosDiagnostics diagnostics)
        {
            this.Attributes.RequestDiagnostics = diagnostics;
        }

        public void AddAttributesToScope()
        {
            this.scope.AddAttribute(CosmosInstrumentationConstants.AccountNameKey, this.Attributes.AccountName.ToString());
            this.scope.AddAttribute(CosmosInstrumentationConstants.ContainerNameKey, this.Attributes.ContainerName);
            this.scope.AddAttribute(CosmosInstrumentationConstants.DbNameKey, this.Attributes.DbName);
            this.scope.AddAttribute(CosmosInstrumentationConstants.DbOperationKey, this.Attributes.DbOperation);
            this.scope.AddAttribute(CosmosInstrumentationConstants.DbSystemKey, this.Attributes.DbSystem);
            this.scope.AddAttribute(CosmosInstrumentationConstants.HttpStatusCodeKey, (int)this.Attributes.HttpStatusCode);
            this.scope.AddAttribute(CosmosInstrumentationConstants.RequestChargeKey, this.Attributes.RequestCharge);
            this.scope.AddAttribute(CosmosInstrumentationConstants.UserAgentKey, this.Attributes.UserAgent);
            this.scope.AddAttribute(CosmosInstrumentationConstants.Region, this.Attributes.Region);
            this.scope.AddAttribute(CosmosInstrumentationConstants.ConnectionMode, this.Attributes.ConnectionMode);
            if (this.Attributes.Error)
            {
                this.scope.AddAttribute(CosmosInstrumentationConstants.ErrorKey, this.Attributes.Error);
                this.scope.AddAttribute(CosmosInstrumentationConstants.ExceptionKey, this.Attributes.ExceptionStackTrace);
            }

            IDiagnosticsFilter filter = new DiagnosticsFilter(this.Attributes);
            if (filter.IsAllowed())
            {
                this.scope.AddAttribute(CosmosInstrumentationConstants.RequestDiagnosticsKey, this.Attributes.RequestDiagnostics);
            }
        }

        public void Dispose()
        {
            this.scope.Dispose();
        }
    }
}
