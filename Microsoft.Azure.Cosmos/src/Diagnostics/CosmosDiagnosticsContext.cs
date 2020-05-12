//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Diagnostics;

    /// <summary>
    /// This represents the diagnostics interface used in the SDK.
    /// </summary>
    internal abstract class CosmosDiagnosticsContext : CosmosDiagnosticsInternal, IEnumerable<CosmosDiagnosticsInternal>
    {
        public abstract DateTime StartUtc { get; }

        public abstract int TotalRequestCount { get; protected set; }

        public abstract int FailedRequestCount { get; protected set; }

        public abstract string UserAgent { get; protected set; }

        public abstract CosmosDiagnostics Diagnostics { get; }

        public abstract IDisposable GetOverallScope();

        public abstract IDisposable CreateScope(string name);

        public abstract IDisposable CreateRequestHandlerScopeScope(RequestHandler requestHandler);

        public abstract TimeSpan GetClientElapsedTime();

        public abstract bool IsComplete();

        public abstract void AddDiagnosticsInternal(PointOperationStatistics pointOperationStatistics);

        public abstract void AddDiagnosticsInternal(QueryPageDiagnostics queryPageDiagnostics);

        public abstract void AddDiagnosticsInternal(StoreResponseStatistics storeResponseStatistics);

        public abstract void AddDiagnosticsInternal(AddressResolutionStatistics addressResolutionStatistics);

        public abstract void AddDiagnosticsInternal(CosmosClientSideRequestStatistics clientSideRequestStatistics);

        public abstract void AddDiagnosticsInternal(FeedRangeStatistics feedRangeStatistics);

        public abstract void AddDiagnosticsInternal(CosmosDiagnosticsContext newContext);

        public abstract void SetSdkUserAgent(string userAgent);

        public abstract IEnumerator<CosmosDiagnosticsInternal> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public static CosmosDiagnosticsContext Create(RequestOptions requestOptions)
        {
            return requestOptions?.DiagnosticContextFactory?.Invoke() ?? new CosmosDiagnosticsContextCore();
        }
    }
}