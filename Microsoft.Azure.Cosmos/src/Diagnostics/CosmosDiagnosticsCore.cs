//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///  Contains the cosmos diagnostic information for the current request to Azure Cosmos DB service.
    /// </summary>
    internal class CosmosDiagnosticsCore : CosmosDiagnostics
    {
        internal CosmosDiagnosticsCore(CosmosDiagnosticsContext diagnosticsContext)
        {
            this.Context = diagnosticsContext ?? throw new ArgumentNullException(nameof(diagnosticsContext));
        }

        internal CosmosDiagnosticsContext Context { get; }

        /// <inheritdoc/>
        public override TimeSpan GetElapsedTime()
        {
            return this.Context.GetElapsedTime();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.Context.ToString();
        }
    }
}
