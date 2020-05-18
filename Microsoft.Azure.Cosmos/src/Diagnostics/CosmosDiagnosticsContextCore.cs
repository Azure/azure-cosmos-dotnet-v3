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
        private readonly List<CosmosDiagnosticsInternal> ContextList;

        private static readonly string DefaultUserAgentString;

        /// <summary>
        /// A stop watch the represent the start of the diagnostics. 
        /// </summary>
        /// <remarks>
        /// The stop watch is never stopped in-case to ensure all operations show correct
        /// elapsed time. This way if a background task is started using a context and that context is completed
        /// the background task will show the correct time once it is completed.
        /// </remarks>
        private readonly Stopwatch Stopwatch;

        private TimeSpan? TotalElapsedTime = null;

        private bool IsDefaultUserAgent = true;

        private bool IsDisposed = false;

        static CosmosDiagnosticsContextCore()
        {
            // Default user agent string does not contain client id or features.
            UserAgentContainer userAgentContainer = new UserAgentContainer();
            CosmosDiagnosticsContextCore.DefaultUserAgentString = userAgentContainer.UserAgent;
        }

        public CosmosDiagnosticsContextCore(string operationName)
        {
            this.StartUtc = DateTime.UtcNow;
            this.ContextList = new List<CosmosDiagnosticsInternal>();
            this.Diagnostics = new CosmosDiagnosticsCore(this);
            this.Stopwatch = Stopwatch.StartNew();
            this.OperationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
        }

        public override DateTime StartUtc { get; }

        public override int TotalRequestCount { get; protected set; }

        public override int FailedRequestCount { get; protected set; }

        public override string UserAgent { get; protected set; } = CosmosDiagnosticsContextCore.DefaultUserAgentString;

        internal override CosmosDiagnostics Diagnostics { get; }

        public override string OperationName { get; }

        internal override TimeSpan GetClientElapsedTime()
        {
            return this.Stopwatch.Elapsed;
        }

        internal override bool TryGetClientTotalElapsedTime(out TimeSpan timeSpan)
        {
            if (this.TotalElapsedTime.HasValue)
            {
                timeSpan = this.TotalElapsedTime.Value;
                return true;
            }

            return false;
        }

        internal override bool IsComplete()
        {
            return this.IsDisposed;
        }

        internal override IDisposable CreateScope(string name)
        {
            CosmosDiagnosticScope scope = new CosmosDiagnosticScope(
                name,
                this.GetClientElapsedTime,
                this.AddEndScope);

            this.ContextList.Add(scope);
            return scope;
        }

        internal override IDisposable CreateRequestHandlerScopeScope(RequestHandler requestHandler)
        {
            return this.CreateScope(requestHandler.GetType().FullName);
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

        internal override void SetSdkUserAgent(string userAgent)
        {
            this.IsDefaultUserAgent = false;
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
            // Using a for loop with a yield prevents Issue #1467 which causes
            // ThrowInvalidOperationException if a new diagnostics is getting added
            // while the enumerator is being used.
            for (int i = 0; i < this.ContextList.Count; i++)
            {
                yield return this.ContextList[i];
            }
        }

        private void AddEndScope(CosmosDiagnosticScopeEnd scopeEnd)
        {
            this.ContextList.Add(scopeEnd);
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

        public override void Dispose()
        {
            if (!this.IsDisposed)
            {
                this.TotalElapsedTime = this.Stopwatch.Elapsed;
                this.IsDisposed = true;
            }
        }
    }
}