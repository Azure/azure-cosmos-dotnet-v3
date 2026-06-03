//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Result of <see cref="MetadataHedgingStrategy.ExecuteAsync"/>.
    /// </summary>
    internal readonly struct MetadataHedgingResult
    {
        public DocumentServiceResponse Response { get; }

        public Uri WinningEndpoint { get; }

        public string WinningRegion { get; }

        public bool HedgeFired { get; }

        public MetadataHedgeDiagnostics Diagnostics { get; }

        public MetadataHedgingResult(
            DocumentServiceResponse response,
            Uri winningEndpoint,
            string winningRegion,
            bool hedgeFired,
            MetadataHedgeDiagnostics diagnostics)
        {
            this.Response = response;
            this.WinningEndpoint = winningEndpoint;
            this.WinningRegion = winningRegion;
            this.HedgeFired = hedgeFired;
            this.Diagnostics = diagnostics;
        }
    }
}
