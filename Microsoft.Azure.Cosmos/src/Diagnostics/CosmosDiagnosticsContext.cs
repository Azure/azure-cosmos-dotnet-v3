//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Text;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// This represents the core diagnostics object used in the SDK.
    /// This object gets created on the initial request and passed down
    /// through the pipeline appending information as it goes into a list
    /// where it is lazily converted to a JSON string.
    /// </summary>
    internal class CosmosDiagnosticsContext : CosmosDiagnostics, ICosmosDiagnosticWriter
    {
        private static readonly string DefaultUserAgentString;

        private readonly DateTime StartUtc;

        private long? retryCount;

        private TimeSpan? retryBackoffTimeSpan;

        private string userAgent;

        private List<ICosmosDiagnosticWriter> contextList { get; }

        static CosmosDiagnosticsContext()
        {
            // Default user agent string does not contain client id or features.
            UserAgentContainer userAgentContainer = new UserAgentContainer();
            CosmosDiagnosticsContext.DefaultUserAgentString = userAgentContainer.UserAgent;
        }

        internal CosmosDiagnosticsContext()
        {
            this.StartUtc = DateTime.UtcNow;
            this.retryCount = null;
            this.retryBackoffTimeSpan = null;
            this.userAgent = CosmosDiagnosticsContext.DefaultUserAgentString;
            this.contextList = new List<ICosmosDiagnosticWriter>(10);
        }

        internal CosmosDiagnosticsScope CreateScope(string name)
        {
            CosmosDiagnosticsScope scope = new CosmosDiagnosticsScope(name);
            this.contextList.Add(scope);
            return scope;
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();        
            this.WriteJsonObject(stringBuilder);

            return stringBuilder.ToString();
        }

        internal void SetSdkUserAgent(string userAgent)
        {
            this.userAgent = userAgent ?? throw new ArgumentNullException(nameof(userAgent));
        }

        internal void AddSdkRetry(TimeSpan backOffTimeSpan)
        {
            if (this.retryBackoffTimeSpan == null)
            {
                this.retryBackoffTimeSpan = TimeSpan.Zero;
            }

            this.retryBackoffTimeSpan = this.retryBackoffTimeSpan.Value.Add(backOffTimeSpan);
            this.retryCount = this.retryCount.GetValueOrDefault(0) + 1;
        }

        internal void AddJsonAttribute(ICosmosDiagnosticWriter diagnosticWriter)
        {
            this.contextList.Add(diagnosticWriter);
        }

        internal void AddJsonAttribute(string key, string value)
        {
            this.contextList.Add(new CosmosDiagnosticsAttribute(key, value));
        }

        public void WriteJsonObject(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"StartUtc\":\"");
            stringBuilder.Append(this.StartUtc);
            stringBuilder.Append("\",\"UserAgent\":\"");
            stringBuilder.Append(this.userAgent);
            if (this.retryCount.HasValue && this.retryCount.Value > 0)
            {
                stringBuilder.Append("\",\"RetryCount\":\"");
                stringBuilder.Append(this.retryCount.Value);
            }

            if (this.retryBackoffTimeSpan.HasValue && this.retryBackoffTimeSpan.Value > TimeSpan.Zero)
            {
                stringBuilder.Append("\",\"RetryBackOffTime\":\"");
                stringBuilder.Append(this.retryBackoffTimeSpan.Value);
            }

            stringBuilder.Append("\",\"Context\":[");
            foreach (ICosmosDiagnosticWriter writer in this.contextList)
            {
                writer.WriteJsonObject(stringBuilder);
                stringBuilder.Append(",");
            }

            // Remove the last comma to make valid json
            if (this.contextList.Count > 0)
            {
                stringBuilder.Length -= 1;
            }

            stringBuilder.Append("]}");
        }

        internal class CosmosDiagnosticsAttribute : ICosmosDiagnosticWriter
        {
            private readonly string key;
            private readonly string value;

            internal CosmosDiagnosticsAttribute(
                string key,
                string value)
            {
                this.key = key;
                this.value = value;
            }

            public void WriteJsonObject(StringBuilder stringBuilder)
            {
                stringBuilder.Append("{\"");
                stringBuilder.Append(this.key);
                stringBuilder.Append("\",\"");
                stringBuilder.Append(this.value);
                stringBuilder.Append("\"}");
            }
        }

        /// <summary>
        /// This represents a single scope in the diagnostics.
        /// A scope is a section of code that is important to track.
        /// For example there is a scope for serialization, retry handlers, etc..
        /// </summary>
        internal class CosmosDiagnosticsScope : ICosmosDiagnosticWriter, IDisposable
        {
            private string Id { get; }

            private Stopwatch ElapsedTimeStopWatch { get; }

            private TimeSpan? ElapsedTime { get; set; }

            internal CosmosDiagnosticsScope(
                string name)
            {
                this.Id = name;
                this.ElapsedTimeStopWatch = Stopwatch.StartNew();
                this.ElapsedTime = null;
            }

            public void Dispose()
            {
                this.ElapsedTimeStopWatch.Stop();
                this.ElapsedTime = this.ElapsedTimeStopWatch.Elapsed;
            }

            public void WriteJsonObject(StringBuilder stringBuilder)
            {
                stringBuilder.Append("{\"Id\":\"");
                stringBuilder.Append(this.Id);
                stringBuilder.Append("\",\"ElapsedTime\":\"");
                if (this.ElapsedTime.HasValue)
                {
                    stringBuilder.Append(this.ElapsedTime.Value);
                }
                else
                {
                    stringBuilder.Append("NoElapsedTime");
                }

                stringBuilder.Append("\"}");
            }
        }
    }
}
