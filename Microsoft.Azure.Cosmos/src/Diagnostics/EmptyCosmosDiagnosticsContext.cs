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

        private EmptyCosmosDiagnosticsContext()
        {
        }

        public override DateTime StartUtc { get; } = new DateTime(0);

        public override int TotalRequestCount { get; protected set; }

        public override int FailedRequestCount { get; protected set; }

        public override TimeSpan? TotalElapsedTime { get; protected set; }

        public override string UserAgent { get; protected set; } = "Empty Context";

        internal override CosmosDiagnosticScope CreateOverallScope(string name)
        {
            return EmptyCosmosDiagnosticsContext.DefaultScope;
        }

        internal override CosmosDiagnosticScope CreateScope(string name)
        {
            return EmptyCosmosDiagnosticsContext.DefaultScope;
        }

        internal override void AddDiagnosticsInternal(PointOperationStatistics pointOperationStatistics)
        {
        }

        internal override void AddDiagnosticsInternal(QueryPageDiagnostics queryPageDiagnostics)
        {
        }

        internal override void AddDiagnosticsInternal(CosmosDiagnosticsContext newContext)
        {
        }

        internal override void AddDiagnosticsInternal(StoreResponseStatistics storeResponseStatistics)
        {
        }

        internal override void AddDiagnosticsInternal(AddressResolutionStatistics addressResolutionStatistics)
        {
        }

        internal override void AddDiagnosticsInternal(CosmosClientSideRequestStatistics clientSideRequestStatistics)
        {
        }

        internal override void SetSdkUserAgent(string userAgent)
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
    }
}