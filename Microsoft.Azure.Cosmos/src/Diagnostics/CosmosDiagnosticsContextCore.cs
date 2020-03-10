//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Diagnostics;

    /// <summary>
    /// This represents the core diagnostics object used in the SDK.
    /// This object gets created on the initial request and passed down
    /// through the pipeline appending information as it goes into a list
    /// where it is lazily converted to a JSON string.
    /// </summary>
    internal sealed class CosmosDiagnosticsContextCore : CosmosDiagnosticsContext
    {
        /// <summary>
        /// Detailed view of all the operations.
        /// </summary>
        private List<CosmosDiagnosticsInternal> ContextList { get; }

        private static readonly string DefaultUserAgentString;

        private readonly bool IsDefaultUserAgent = true;

        private bool isOverallScopeSet = false;

        static CosmosDiagnosticsContextCore()
        {
            // Default user agent string does not contain client id or features.
            UserAgentContainer userAgentContainer = new UserAgentContainer();
            CosmosDiagnosticsContextCore.DefaultUserAgentString = userAgentContainer.UserAgent;
        }

        public CosmosDiagnosticsContextCore(string userClientRequestId)
        {
            this.StartUtc = DateTime.UtcNow;
            this.UserClientRequestId = userClientRequestId;
            this.ContextList = new List<CosmosDiagnosticsInternal>();
            this.Diagnostics = new CosmosDiagnosticsCore(this);
            this.OverallClientRequestTime = Stopwatch.StartNew();
        }

        public override DateTime StartUtc { get; }

        public override int TotalRequestCount { get; protected set; }

        public override int FailedRequestCount { get; protected set; }

        public override string UserAgent { get; protected set; } = CosmosDiagnosticsContextCore.DefaultUserAgentString;

        public override string UserClientRequestId { get; }

        internal override CosmosDiagnostics Diagnostics { get; }

        public override Stopwatch OverallClientRequestTime { get; }

        internal override CosmosDiagnosticScope CreateOverallScope(string name)
        {
            CosmosDiagnosticScope scope;
            // If overall is already set then let the original set the elapsed time.
            if (this.isOverallScopeSet)
            {
                scope = new CosmosDiagnosticScope(name);
            }
            else
            {
                this.isOverallScopeSet = true;
                scope = new CosmosDiagnosticScope(name, this.OverallClientRequestTime);
            }

            this.ContextList.Add(scope);
            return scope;
        }

        internal override CosmosDiagnosticScope CreateScope(string name)
        {
            CosmosDiagnosticScope scope = new CosmosDiagnosticScope(name);

            this.ContextList.Add(scope);
            return scope;
        }

        internal override void AddDiagnosticsInternal(PointOperationStatistics pointOperationStatistics)
        {
            if (pointOperationStatistics == null)
            {
                throw new ArgumentNullException(nameof(pointOperationStatistics));
            }

            this.AddRequestCount((int)pointOperationStatistics.StatusCode);

            this.ContextList.Add(pointOperationStatistics);
        }

        internal override void AddDiagnosticsInternal(StoreResponseStatistics storeResponseStatistics)
        {
            if (storeResponseStatistics.StoreResult != null)
            {
                this.AddRequestCount((int)storeResponseStatistics.StoreResult.StatusCode);
            }

            this.ContextList.Add(storeResponseStatistics);
        }

        internal override void AddDiagnosticsInternal(AddressResolutionStatistics addressResolutionStatistics)
        {
            this.ContextList.Add(addressResolutionStatistics);
        }

        internal override void AddDiagnosticsInternal(CosmosClientSideRequestStatistics clientSideRequestStatistics)
        {
            this.ContextList.Add(clientSideRequestStatistics);
        }

        internal override void AddDiagnosticsInternal(QueryPageDiagnostics queryPageDiagnostics)
        {
            if (queryPageDiagnostics == null)
            {
                throw new ArgumentNullException(nameof(queryPageDiagnostics));
            }

            if (queryPageDiagnostics.DiagnosticsContext != null)
            {
                this.AddSummaryInfo(queryPageDiagnostics.DiagnosticsContext);
            }

            this.ContextList.Add(queryPageDiagnostics);
        }

        internal override void AddDiagnosticsInternal(CosmosDiagnosticsContext newContext)
        {
            this.AddSummaryInfo(newContext);

            this.ContextList.AddRange(newContext);
        }

        internal override void SetSdkUserAgent(string userAgent)
        {
            this.UserAgent = userAgent;
        }

        public override void Accept(CosmosDiagnosticsInternalVisitor cosmosDiagnosticsInternalVisitor)
        {
            cosmosDiagnosticsInternalVisitor.Visit(this);
        }

        public override TResult Accept<TResult>(CosmosDiagnosticsInternalVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public override IEnumerator<CosmosDiagnosticsInternal> GetEnumerator()
        {
            return this.ContextList.GetEnumerator();
        }

        private void AddRequestCount(int statusCode)
        {
            this.TotalRequestCount++;
            if (statusCode < 200 || statusCode > 299)
            {
                this.FailedRequestCount++;
            }
        }

        private void AddSummaryInfo(CosmosDiagnosticsContext newContext)
        {
            if (Object.ReferenceEquals(this, newContext))
            {
                return;
            }

            if (this.IsDefaultUserAgent && newContext.UserAgent != null)
            {
                this.SetSdkUserAgent(newContext.UserAgent);
            }

            this.TotalRequestCount += newContext.TotalRequestCount;
            this.FailedRequestCount += newContext.FailedRequestCount;
        }
    }
}