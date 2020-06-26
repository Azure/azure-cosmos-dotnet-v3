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
    using System.Security.Policy;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// This represents the core diagnostics object used in the SDK.
    /// This object gets created on the initial request and passed down
    /// through the pipeline appending information as it goes into a list
    /// where it is lazily converted to a JSON string.
    /// </summary>
    internal sealed class CosmosDiagnosticsContextCore : CosmosDiagnosticsContext
    {
        private readonly Stack<CosmosDiagnosticScope> CurrentScope;
        private static readonly string DefaultUserAgentString;

        private readonly CosmosDiagnosticScope overallScope;

        static CosmosDiagnosticsContextCore()
        {
            // Default user agent string does not contain client id or features.
            UserAgentContainer userAgentContainer = new UserAgentContainer();
            CosmosDiagnosticsContextCore.DefaultUserAgentString = userAgentContainer.UserAgent;
        }

        public CosmosDiagnosticsContextCore()
            : this(nameof(CosmosDiagnosticsContextCore),
                  CosmosDiagnosticsContextCore.DefaultUserAgentString)
        {
        }

        public CosmosDiagnosticsContextCore(
            string operationName,
            string userAgentString)
        {
            this.UserAgent = userAgentString ?? throw new ArgumentNullException(nameof(userAgentString));
            this.OperationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
            this.StartUtc = DateTime.UtcNow;
            this.CurrentScope = new Stack<CosmosDiagnosticScope>();
            this.Diagnostics = new CosmosDiagnosticsCore(this);
            this.overallScope = new CosmosDiagnosticScope("Overall", this.UpdateCurrentScope);
            this.CurrentScope.Push(this.overallScope);
        }

        public override DateTime StartUtc { get; }

        public override string UserAgent { get; }

        public override string OperationName { get; }

        internal override CosmosDiagnostics Diagnostics { get; }

        internal override IDisposable GetOverallScope()
        {
            return this.overallScope;
        }

        internal override TimeSpan GetRunningElapsedTime()
        {
            return this.overallScope.GetElapsedTime();
        }

        internal override bool TryGetTotalElapsedTime(out TimeSpan timeSpan)
        {
            return this.overallScope.TryGetElapsedTime(out timeSpan);
        }

        internal override bool IsComplete()
        {
            return this.overallScope.IsComplete();
        }

        public override int GetTotalRequestCount()
        {
            return this.totalRequestCount;
        }

        public override int GetFailedRequestCount()
        {
            return this.failedRequestCount;
        }

        internal override IDisposable CreateScope(string name)
        {
            CosmosDiagnosticScope scope = new CosmosDiagnosticScope(name, this.UpdateCurrentScope);

            this.CurrentScope.Peek().AddDiagnosticsInternal(scope);
            this.CurrentScope.Push(scope);
            return scope;
        }

        internal override IDisposable CreateRequestHandlerScopeScope(RequestHandler requestHandler)
        {
            if (requestHandler == null)
            {
                throw new ArgumentNullException(nameof(requestHandler));
            }

            return this.CreateScope(requestHandler.GetType().FullName);
        }

        internal override void AddDiagnosticsInternal(CosmosSystemInfo processInfo)
        {
            if (processInfo == null)
            {
                throw new ArgumentNullException(nameof(processInfo));
            }

            this.CurrentScope.Peek().AddDiagnosticsInternal(processInfo);
        }

        internal override void AddDiagnosticsInternal(PointOperationStatistics pointOperationStatistics)
        {
            if (pointOperationStatistics == null)
            {
                throw new ArgumentNullException(nameof(pointOperationStatistics));
            }

            this.AddRequestCount((int)pointOperationStatistics.StatusCode);

            this.CurrentScope.Peek().AddDiagnosticsInternal(pointOperationStatistics);
        }

        internal override void AddDiagnosticsInternal(StoreResponseStatistics storeResponseStatistics)
        {
            if (storeResponseStatistics.StoreResult != null)
            {
                this.AddRequestCount((int)storeResponseStatistics.StoreResult.StatusCode);
            }

            this.CurrentScope.Peek().AddDiagnosticsInternal(storeResponseStatistics);
        }

        internal override void AddDiagnosticsInternal(AddressResolutionStatistics addressResolutionStatistics)
        {
            this.CurrentScope.Peek().AddDiagnosticsInternal(addressResolutionStatistics);
        }

        internal override void AddDiagnosticsInternal(CosmosClientSideRequestStatistics clientSideRequestStatistics)
        {
            this.CurrentScope.Peek().AddDiagnosticsInternal(clientSideRequestStatistics);
        }

        internal override void AddDiagnosticsInternal(FeedRangeStatistics feedRangeStatistics)
        {
            this.CurrentScope.Peek().AddDiagnosticsInternal(feedRangeStatistics);
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

            this.CurrentScope.Peek().AddDiagnosticsInternal(queryPageDiagnostics);
        }

        internal override void AddDiagnosticsInternal(CosmosDiagnosticsContext newContext)
        {
            this.AddSummaryInfo(newContext);

            this.CurrentScope.Peek().AddDiagnosticsInternal(newContext);
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
            // Using a for loop with a yield prevents Issue #1467 which causes
            // ThrowInvalidOperationException if a new diagnostics is getting added
            // while the enumerator is being used.
            return this.overallScope.GetEnumerator();
        }

        private void UpdateCurrentScope()
        {
            this.CurrentScope.Pop();
        }

        private void AddRequestCount(int statusCode)
        {
            this.totalRequestCount++;
            if (statusCode < 200 || statusCode > 299)
            {
                this.failedRequestCount++;
            }
        }

        private void AddSummaryInfo(CosmosDiagnosticsContext newContext)
        {
            if (Object.ReferenceEquals(this, newContext))
            {
                return;
            }

            this.totalRequestCount += newContext.GetTotalRequestCount();
            this.failedRequestCount += newContext.GetFailedRequestCount();
        }
    }
}
