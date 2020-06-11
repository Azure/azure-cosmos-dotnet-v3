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
    internal abstract class CosmosDiagnosticsContext : CosmosDiagnosticsInternal, IEnumerable<CosmosDiagnosticsInternal>
    {
        public abstract DateTime StartUtc { get; }

        public abstract int TotalRequestCount { get; protected set; }

        public abstract int FailedRequestCount { get; protected set; }

        public abstract string UserAgent { get; protected set; }

        internal abstract CosmosDiagnostics Diagnostics { get; }

        internal abstract IDisposable GetOverallScope();

        internal abstract IDisposable CreateScope(string name);

        internal abstract IDisposable CreateRequestHandlerScopeScope(RequestHandler requestHandler);

        internal abstract TimeSpan GetClientElapsedTime();

        internal abstract bool IsComplete();

        internal abstract void AddDiagnosticsInternal(CosmosSystemInfo cpuLoadHistory);

        internal abstract void AddDiagnosticsInternal(PointOperationStatistics pointOperationStatistics);

        internal abstract void AddDiagnosticsInternal(QueryPageDiagnostics queryPageDiagnostics);

        internal abstract void AddDiagnosticsInternal(StoreResponseStatistics storeResponseStatistics);

        internal abstract void AddDiagnosticsInternal(AddressResolutionStatistics addressResolutionStatistics);

        internal abstract void AddDiagnosticsInternal(CosmosClientSideRequestStatistics clientSideRequestStatistics);

        internal abstract void AddDiagnosticsInternal(FeedRangeStatistics feedRangeStatistics);

        internal abstract void AddDiagnosticsInternal(CosmosDiagnosticsContext newContext);

        internal abstract void SetSdkUserAgent(string userAgent);

        public abstract IEnumerator<CosmosDiagnosticsInternal> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        internal static CosmosDiagnosticsContext Create(RequestOptions requestOptions)
        {
            return requestOptions?.DiagnosticContextFactory?.Invoke() ?? new CosmosDiagnosticsContextCore();
        }
    }
}