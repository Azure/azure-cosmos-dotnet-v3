//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Diagnostics;
    using Cosmos.Diagnostics;
    using global::Azure.Core.Pipeline;
    using Tracing;

    internal class CosmosInstrumentation : ICosmosInstrumentation
    {
        private readonly DiagnosticScope scope;
        private readonly bool isEnabled = false;
      
        public CosmosInstrumentation(DiagnosticScope scope)
        {
            this.scope = scope;
            this.scope.Start();

            this.isEnabled = Activity.Current != null && this.scope.IsEnabled && Activity.Current.IsAllDataRequested;
        }
        
        public void MarkFailed(Exception ex)
        {
            if (this.isEnabled)
            {
                this.scope.AddAttribute("error", true);
                this.scope.AddAttribute("Exception", ex.StackTrace);
                
                this.scope.Failed(ex);
            }
        }

        public void MarkDone(ITrace trace, DiagnosticAttributes attributes)
        {
            if (this.isEnabled)
            {
                this.AddAttributes(trace, attributes);
            }
        }

        public void AddAttribute(string key, object value)
        {
            this.scope.AddAttribute(key, value);
        }

        private void AddAttributes(ITrace trace, DiagnosticAttributes attributes)
        {
            this.scope.AddAttribute("db.system", "cosmosdb");
            this.scope.AddAttribute("db.name", attributes.DatabaseId);
            this.scope.AddAttribute("db.operation", attributes.OperationType);

            this.scope.AddAttribute("http.status_code", attributes.StatusCode);

            this.scope.AddAttribute("Container Name", attributes.ContainerId);
            this.scope.AddAttribute("Account Name", attributes.AccountName);
            this.scope.AddAttribute("User Agent", attributes.UserAgent);
            this.scope.AddAttribute("Request Charge (RUs)", attributes.RequestCharge);

            this.scope.AddAttribute("Request Diagnostics (JSON)", new CosmosTraceDiagnostics(trace));
        }

        public void Dispose()
        {
            this.scope.Dispose();
        }
    }
}
