//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Diagnostics;

    /// <summary>
    /// This represents a disabled diagnostics context. This is used when the diagnostics
    /// should not be recorded to avoid the overhead of the data collection.
    /// </summary>
    internal sealed class EmptyCosmosDiagnosticsContext : CosmosDiagnosticsContext
    {
        private static readonly IReadOnlyList<CosmosDiagnosticsInternal> EmptyList = new List<CosmosDiagnosticsInternal>();
        private static readonly CosmosDiagnosticScope DefaultScope = new CosmosDiagnosticScope("DisabledScope");
        public static readonly CosmosDiagnosticsContext Singleton = new EmptyCosmosDiagnosticsContext();

        private static readonly DateTime DefaultStartUtc = DateTime.MinValue;

        private EmptyCosmosDiagnosticsContext()
        {
            this.Diagnostics = new CosmosDiagnosticsCore(this);
        }

        public override DateTime StartUtc { get; } = EmptyCosmosDiagnosticsContext.DefaultStartUtc;

        public override int TotalRequestCount { get; protected set; }

        public override int FailedRequestCount { get; protected set; }

        public override string UserAgent { get; protected set; } = "Empty Context";

        public override CosmosDiagnostics Diagnostics { get; }

        public override IDisposable GetOverallScope()
        {
            return EmptyCosmosDiagnosticsContext.DefaultScope;
        }

        public override IDisposable CreateScope(string name)
        {
            return EmptyCosmosDiagnosticsContext.DefaultScope;
        }

        public override IDisposable CreateRequestHandlerScopeScope(RequestHandler requestHandler)
        {
            return EmptyCosmosDiagnosticsContext.DefaultScope;
        }

        public override void AddDiagnosticsInternal(PointOperationStatistics pointOperationStatistics)
        {
        }

        public override void AddDiagnosticsInternal(QueryPageDiagnostics queryPageDiagnostics)
        {
        }

        public override void AddDiagnosticsInternal(CosmosDiagnosticsContext newContext)
        {
        }

        public override void AddDiagnosticsInternal(StoreResponseStatistics storeResponseStatistics)
        {
        }

        public override void AddDiagnosticsInternal(AddressResolutionStatistics addressResolutionStatistics)
        {
        }

        public override void AddDiagnosticsInternal(CosmosClientSideRequestStatistics clientSideRequestStatistics)
        {
        }

        public override void AddDiagnosticsInternal(FeedRangeStatistics feedRangeStatistics)
        {
        }

        public override void SetSdkUserAgent(string userAgent)
        {
        }

        public override void Accept(CosmosDiagnosticsInternalVisitor cosmosDiagnosticsInternalVisitor)
        {
        }

        public override TResult Accept<TResult>(CosmosDiagnosticsInternalVisitor<TResult> visitor)
        {
            return default;
        }

        public override IEnumerator<CosmosDiagnosticsInternal> GetEnumerator()
        {
            return EmptyCosmosDiagnosticsContext.EmptyList.GetEnumerator();
        }

        public override TimeSpan GetClientElapsedTime()
        {
            return TimeSpan.Zero;
        }

        public override bool IsComplete()
        {
            return true;
        }
    }
}