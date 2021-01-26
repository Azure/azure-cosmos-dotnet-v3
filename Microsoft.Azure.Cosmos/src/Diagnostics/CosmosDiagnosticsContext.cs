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

        public abstract string UserAgent { get; }

        public abstract string OperationName { get; }

        internal abstract CosmosDiagnostics Diagnostics { get; }

        public abstract int GetTotalResponseCount();

        public abstract int GetFailedResponseCount();

        public abstract int GetRetriableResponseCount();

        internal abstract IDisposable GetOverallScope();

        internal abstract IDisposable CreateScope(string name);

        internal abstract IDisposable CreateRequestHandlerScopeScope(RequestHandler requestHandler);

        internal abstract TimeSpan GetRunningElapsedTime();

        internal abstract bool TryGetTotalElapsedTime(out TimeSpan timeSpan);

        internal abstract bool IsComplete();

        internal abstract void AddDiagnosticsInternal(CosmosSystemInfo cpuLoadHistory);

        internal abstract void AddDiagnosticsInternal(PointOperationStatistics pointOperationStatistics);

        internal abstract void AddDiagnosticsInternal(QueryPageDiagnostics queryPageDiagnostics);

        internal abstract void AddDiagnosticsInternal(StoreResponseStatistics storeResponseStatistics);

        internal abstract void AddDiagnosticsInternal(AddressResolutionStatistics addressResolutionStatistics);

        internal abstract void AddDiagnosticsInternal(CosmosClientSideRequestStatistics clientSideRequestStatistics);

        internal abstract void AddDiagnosticsInternal(FeedRangeStatistics feedRangeStatistics);

        internal abstract void AddDiagnosticsInternal(CosmosDiagnosticsContext newContext);

        public abstract IEnumerator<CosmosDiagnosticsInternal> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        internal static CosmosDiagnosticsContext Create(
            RequestOptions requestOptions)
        {
            return EmptyCosmosDiagnosticsContext.Singleton;
        }

        internal static CosmosDiagnosticsContext Create(
            string operationName,
            RequestOptions requestOptions,
            string userAgentString)
        {
            return EmptyCosmosDiagnosticsContext.Singleton;
        }
    }
}