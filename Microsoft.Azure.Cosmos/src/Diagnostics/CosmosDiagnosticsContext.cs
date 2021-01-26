//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// This represents the diagnostics interface used in the SDK.
    /// </summary>
#pragma warning disable SA1302 // Interface names should begin with I
    internal interface CosmosDiagnosticsContext : IEnumerable<CosmosDiagnosticsInternal>
#pragma warning restore SA1302 // Interface names should begin with I
    {
        public DateTime StartUtc { get; }

        public string UserAgent { get; }

        public string OperationName { get; }

        internal CosmosDiagnostics Diagnostics { get; }

        public int GetTotalResponseCount();

        public int GetFailedResponseCount();

        public int GetRetriableResponseCount();

        internal IDisposable GetOverallScope();

        internal IDisposable CreateScope(string name);

        internal IDisposable CreateRequestHandlerScopeScope(RequestHandler requestHandler);

        internal TimeSpan GetRunningElapsedTime();

        internal bool TryGetTotalElapsedTime(out TimeSpan timeSpan);

        internal bool IsComplete();

        internal void AddDiagnosticsInternal(CosmosSystemInfo cpuLoadHistory);

        internal void AddDiagnosticsInternal(PointOperationStatistics pointOperationStatistics);

        internal void AddDiagnosticsInternal(QueryPageDiagnostics queryPageDiagnostics);

        internal void AddDiagnosticsInternal(StoreResponseStatistics storeResponseStatistics);

        internal void AddDiagnosticsInternal(AddressResolutionStatistics addressResolutionStatistics);

        internal void AddDiagnosticsInternal(CosmosClientSideRequestStatistics clientSideRequestStatistics);

        internal void AddDiagnosticsInternal(FeedRangeStatistics feedRangeStatistics);

        internal void AddDiagnosticsInternal(CosmosDiagnosticsContext newContext);
    }
}