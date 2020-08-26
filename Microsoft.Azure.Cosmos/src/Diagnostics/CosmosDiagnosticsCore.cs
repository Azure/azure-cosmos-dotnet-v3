//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;

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
        public override TimeSpan GetClientElapsedTime()
        {
            if (this.Context.TryGetTotalElapsedTime(out TimeSpan timeSpan))
            {
                return timeSpan;
            }

            return this.Context.GetRunningElapsedTime();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.Context.ToString();
        }
    }
}
