//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Diagnostics;

    /// <summary>
    /// This represents the core diagnostics object used in the SDK.
    /// This object gets created on the initial request and passed down
    /// through the pipeline appending information as it goes into a list
    /// where it is lazily converted to a JSON string.
    /// </summary>
    internal sealed class CosmosDiagnosticsContextCore : CosmosDiagnosticsContext
    {
        private static readonly string DefaultUserAgentString;
        private readonly CosmosDiagnosticScope overallScope;

        /// <summary>
        /// Detailed view of all the operations.
        /// </summary>
        private List<CosmosDiagnosticsInternal> ContextList { get; }

        private int totalRequestCount = 0;
        private int failedRequestCount = 0;

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
            this.ContextList = new List<CosmosDiagnosticsInternal>();
            this.Diagnostics = new CosmosDiagnosticsCore(this);
            this.overallScope = new CosmosDiagnosticScope("Overall");
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
            CosmosDiagnosticScope scope = new CosmosDiagnosticScope(name);

            this.ContextList.Add(scope);
            return scope;
        }

        internal override IDisposable CreateRequestHandlerScopeScope(RequestHandler requestHandler)
        {
            RequestHandlerScope requestHandlerScope = new RequestHandlerScope(requestHandler);
            this.ContextList.Add(requestHandlerScope);
            return requestHandlerScope;
        }

        internal override void AddDiagnosticsInternal(CosmosSystemInfo processInfo)
        {
            if (processInfo == null)
            {
                throw new ArgumentNullException(nameof(processInfo));
            }

            this.ContextList.Add(processInfo);
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

        internal override void AddDiagnosticsInternal(FeedRangeStatistics feedRangeStatistics)
        {
            this.ContextList.Add(feedRangeStatistics);
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
            for (int i = 0; i < this.ContextList.Count; i++)
            {
                yield return this.ContextList[i];
            }
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
