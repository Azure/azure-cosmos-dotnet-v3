//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Diagnostics;

    /// <summary>
    /// This represents a disabled diagnostics context. This is used when the diagnostics
    /// should not be recorded to avoid the overhead of the data collection.
    /// </summary>
    internal sealed class EmptyCosmosDiagnosticsContext : CosmosDiagnosticsContext
    {
        public static readonly CosmosDiagnosticsContext Singleton = new EmptyCosmosDiagnosticsContext();

        private EmptyCosmosDiagnosticsContext()
        {
        }

        public DateTime StartUtc => DateTime.UtcNow;

        public string UserAgent => string.Empty;

        public string OperationName => string.Empty;

        CosmosDiagnostics CosmosDiagnosticsContext.Diagnostics => null;

        public IEnumerator<CosmosDiagnosticsInternal> GetEnumerator()
        {
            return Enumerable.Empty<CosmosDiagnosticsInternal>().GetEnumerator();
        }

        public int GetFailedResponseCount()
        {
            return 0;
        }

        public int GetRetriableResponseCount()
        {
            return 0;
        }

        public int GetTotalResponseCount()
        {
            return 0;
        }

        void CosmosDiagnosticsContext.AddDiagnosticsInternal(CosmosSystemInfo cpuLoadHistory)
        {
        }

        void CosmosDiagnosticsContext.AddDiagnosticsInternal(PointOperationStatistics pointOperationStatistics)
        {
        }

        void CosmosDiagnosticsContext.AddDiagnosticsInternal(QueryPageDiagnostics queryPageDiagnostics)
        {
        }

        void CosmosDiagnosticsContext.AddDiagnosticsInternal(StoreResponseStatistics storeResponseStatistics)
        {
        }

        void CosmosDiagnosticsContext.AddDiagnosticsInternal(AddressResolutionStatistics addressResolutionStatistics)
        {
        }

        void CosmosDiagnosticsContext.AddDiagnosticsInternal(CosmosClientSideRequestStatistics clientSideRequestStatistics)
        {
        }

        void CosmosDiagnosticsContext.AddDiagnosticsInternal(FeedRangeStatistics feedRangeStatistics)
        {
        }

        void CosmosDiagnosticsContext.AddDiagnosticsInternal(CosmosDiagnosticsContext newContext)
        {
        }

        IDisposable CosmosDiagnosticsContext.CreateRequestHandlerScopeScope(RequestHandler requestHandler)
        {
            return NoOp.Singleton;
        }

        IDisposable CosmosDiagnosticsContext.CreateScope(string name)
        {
            return NoOp.Singleton;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Enumerable.Empty<CosmosDiagnosticsInternal>().GetEnumerator();
        }

        IDisposable CosmosDiagnosticsContext.GetOverallScope()
        {
            return NoOp.Singleton;
        }

        TimeSpan CosmosDiagnosticsContext.GetRunningElapsedTime()
        {
            return TimeSpan.Zero;
        }

        bool CosmosDiagnosticsContext.IsComplete()
        {
            return true;
        }

        bool CosmosDiagnosticsContext.TryGetTotalElapsedTime(out TimeSpan timeSpan)
        {
            return false;
        }

        private class NoOp : IDisposable
        {
            public static readonly IDisposable Singleton = new NoOp();

            public void Dispose()
            {
            }
        }
    }
}