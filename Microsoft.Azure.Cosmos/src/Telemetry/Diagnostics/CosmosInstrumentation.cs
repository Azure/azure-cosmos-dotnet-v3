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
            this.Attributes.Error = true;
            this.Attributes.ExceptionStackTrace = ex.StackTrace;
            
            this.scope.Failed(ex);
        }

        public void Record(double? requestCharge = null,
            string operationType = null,
            HttpStatusCode? statusCode = null, 
            string databaseId = null, 
            string containerId = null,
            string queryText = null,
            string subStatusCode = null,
            string pageSize = null)
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
                    this.Attributes.SubStatusCode = subStatusCode;
                }

                if (pageSize != null)
                {
                    this.Attributes.PageSize = pageSize;
                }

                if (requestCharge.HasValue)
                {
                    this.Attributes.RequestCharge = requestCharge.Value;
                } 

            }
        }

        public void RecordWithException(double? requestCharge,
           string operationType,
           HttpStatusCode? statusCode,
           string databaseId,
           string containerId,
           Exception exception,
           string queryText = null,
           string subStatusCode = null,
           string pageSize = null)
        {
            this.Record(
                requestCharge: requestCharge,
                operationType: operationType,
                statusCode: statusCode,
                databaseId: databaseId,
                containerId: containerId,
                queryText: queryText,
                subStatusCode: subStatusCode,
                pageSize: pageSize);

            if (this.Attributes != null)
            {
                this.Attributes.Error = true;
                this.Attributes.ExceptionStackTrace = exception.StackTrace;
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

            this.scope.AddAttribute(CosmosInstrumentationConstants.AccountNameKey, this.Attributes.AccountName?.ToString());
            this.scope.AddAttribute(CosmosInstrumentationConstants.ContainerNameKey, this.Attributes.ContainerName);
            this.scope.AddAttribute(CosmosInstrumentationConstants.DbNameKey, this.Attributes.DbName);
            this.scope.AddAttribute(CosmosInstrumentationConstants.DbOperationKey, this.Attributes.DbOperation);
            this.scope.AddAttribute(CosmosInstrumentationConstants.DbSystemKey, this.Attributes.DbSystem);
            if (this.Attributes.HttpStatusCode.HasValue)
            {
                this.scope.AddAttribute(CosmosInstrumentationConstants.HttpStatusCodeKey, (int)this.Attributes.HttpStatusCode.Value);
            }

            if (this.Attributes.RequestCharge.HasValue)
            {
                this.scope.AddAttribute(CosmosInstrumentationConstants.RequestChargeKey, this.Attributes.RequestCharge.Value);

            }
            this.scope.AddAttribute(CosmosInstrumentationConstants.UserAgentKey, this.Attributes.UserAgent);
            this.scope.AddAttribute(CosmosInstrumentationConstants.ConnectionMode, this.Attributes.ConnectionMode);
            
            this.scope.AddAttribute(CosmosInstrumentationConstants.PageSize, this.Attributes.PageSize);
            this.scope.AddAttribute(CosmosInstrumentationConstants.SubStatusCode, this.Attributes.SubStatusCode);

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
            this.AddAttributesToScope();
            this.scope.Dispose();
        }
    }
}
