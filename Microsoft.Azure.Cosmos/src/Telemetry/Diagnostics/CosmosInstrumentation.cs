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
      
        public CosmosInstrumentation(DiagnosticScope scope)
        {
            this.scope = scope;

            this.scope.Start();
        }
        
        public void MarkFailed(Exception ex)
        {
            this.scope.AddAttribute("error", true);
            this.scope.AddAttribute("Exception", ex.StackTrace);
            
            this.scope.Failed(ex);
        }

        public void AddAttribute(string key, object value)
        {
            this.scope.AddAttribute(key, value);
        }

        public void Dispose()
        {
            this.scope.Dispose();
        }
    }
}
