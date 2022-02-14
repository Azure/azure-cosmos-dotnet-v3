//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using global::Azure.Core.Pipeline;

    internal class CosmosInstrumentation : ICosmosInstrumentation
    {
        private readonly DiagnosticScope scope;

        public DiagnosticAttributes Attributes { get; }

        public CosmosInstrumentation(DiagnosticScope scope, DiagnosticAttributes attributes)
        {
            this.scope = scope;
            this.Attributes = attributes;

            this.scope.Start();
        }

        public void MarkFailed(Exception ex)
        {
            this.Attributes.Error = true;
            this.Attributes.ExceptionStackTrace = ex.StackTrace;
            
            this.scope.Failed(ex);
        }

        public void AddAttributesToScope()
        {
            this.scope.AddAttribute(CosmosInstrumentationConstants.AccountNameKey, this.Attributes.AccountName.ToString());
            this.scope.AddAttribute(CosmosInstrumentationConstants.ContainerNameKey, this.Attributes.ContainerName);
            this.scope.AddAttribute(CosmosInstrumentationConstants.DbNameKey, this.Attributes.DbName);
            this.scope.AddAttribute(CosmosInstrumentationConstants.DbOperationKey, this.Attributes.DbOperation);
            this.scope.AddAttribute(CosmosInstrumentationConstants.DbSystemKey, this.Attributes.DbSystem);
            this.scope.AddAttribute(CosmosInstrumentationConstants.HttpStatusCodeKey, this.Attributes.HttpStatusCode);
            this.scope.AddAttribute(CosmosInstrumentationConstants.RequestChargeKey, this.Attributes.RequestCharge);
            this.scope.AddAttribute(CosmosInstrumentationConstants.UserAgentKey, this.Attributes.UserAgent);
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
