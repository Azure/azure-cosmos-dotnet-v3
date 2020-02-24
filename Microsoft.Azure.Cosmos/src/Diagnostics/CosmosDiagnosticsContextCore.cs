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

        private bool IsDefaultUserAgent = true;

        private bool isOverallScopeSet = false;

        static CosmosDiagnosticsContextCore()
        {
            // Default user agent string does not contain client id or features.
            UserAgentContainer userAgentContainer = new UserAgentContainer();
            CosmosDiagnosticsContextCore.DefaultUserAgentString = userAgentContainer.UserAgent;
        }

        public CosmosDiagnosticsContextCore()
        {
            this.StartUtc = DateTime.UtcNow;
            this.ContextList = new List<CosmosDiagnosticsInternal>();
        }

        public override DateTime StartUtc { get; }

        public override int TotalRequestCount { get; protected set; }

        public override int FailedRequestCount { get; protected set; }

        public override TimeSpan? TotalElapsedTime { get; protected set; }

        public override string UserAgent { get; protected set; } = CosmosDiagnosticsContextCore.DefaultUserAgentString;

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
                scope = new CosmosDiagnosticScope(name, this.SetElapsedTime);
                this.isOverallScopeSet = true;
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

            this.TotalRequestCount++;
            int statusCode = (int)pointOperationStatistics.StatusCode;
            if (statusCode < 200 || statusCode > 299)
            {
                this.FailedRequestCount++;
            }

            this.ContextList.Add(pointOperationStatistics);
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

        internal override void AddDiagnosticsInternal(QueryPipelineDiagnostics queryPipelineDiagnostics)
        {
            if (queryPipelineDiagnostics == null)
            {
                throw new ArgumentNullException(nameof(queryPipelineDiagnostics));
            }

            this.ContextList.Add(queryPipelineDiagnostics);
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

        private void SetElapsedTime(TimeSpan totalElapsedTime)
        {
            this.TotalElapsedTime = totalElapsedTime;
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

            // Use the larger of the total elapsed times
            if (this.TotalElapsedTime < newContext.TotalElapsedTime)
            {
                this.TotalElapsedTime = newContext.TotalElapsedTime;
            }

            this.TotalRequestCount += newContext.TotalRequestCount;
            this.FailedRequestCount += newContext.FailedRequestCount;
        }
    }
}