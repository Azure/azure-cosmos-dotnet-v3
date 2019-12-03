//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;

    /// <summary>
    /// This repesents the core diagnostics object used in the SDK.
    /// This object gets created on the initial request and passed down
    /// through the pipeline appending information as it goes into a list
    /// where it is lazily converted to a JSON string.
    /// </summary>
    internal class CosmosDiagnosticsCore : CosmosDiagnostics
    {
        private static string DefaultUserAgentString { get; }
        private List<ICosmosDiagnosticsJsonWriter> scopes { get; } = new List<ICosmosDiagnosticsJsonWriter>();
        private long retryCount = 0;
        private TimeSpan retryBackoffTimeSpan;
        private string userAgent;

        static CosmosDiagnosticsCore()
        {
            // Default user agent string does not contain client id or features.
            UserAgentContainer userAgentContainer = new UserAgentContainer();
            CosmosDiagnosticsCore.DefaultUserAgentString = userAgentContainer.UserAgent;
        }

        internal CosmosDiagnosticsCore()
        {
            this.userAgent = CosmosDiagnosticsCore.DefaultUserAgentString;
        }

        internal CosmosDiagnosticsScope CreateScope(string name)
        {
            CosmosDiagnosticsScope scope = new CosmosDiagnosticsScope(this, name);
            this.scopes.Add(scope);
            return scope;
        }

        internal void JoinScopes(CosmosDiagnosticsCore leafScope)
        {
            this.scopes.AddRange(leafScope.scopes);
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("{");
            this.AppendJsonRetryInfo(builder);
            builder.Append(",");
            this.AppendJsonUserAgentInfo(builder);
            if (this.scopes != null && this.scopes.Count > 0)
            {
                foreach (ICosmosDiagnosticsJsonWriter scope in this.scopes)
                {
                    builder.Append(",");
                    scope.AppendJson(builder); 
                }
            }
            builder.Append("}");

            return builder.ToString();
        }

        internal void SetSdkUserAgent(string userAgent)
        {
            this.userAgent = userAgent ?? throw new ArgumentNullException(nameof(userAgent));
        }

        internal void AddSdkRetry(TimeSpan backOffTimeSpan)
        {
            if (this.retryBackoffTimeSpan == null)
            {
                this.retryBackoffTimeSpan = new TimeSpan();
            }

            this.retryBackoffTimeSpan.Add(backOffTimeSpan);
            this.retryCount++;
        }

        internal void AddScope(CosmosDiagnosticsScope diagnosticsScope)
        {
            this.scopes.Add(diagnosticsScope);
        }

        internal void AddJsonAttribute(string name, ICosmosDiagnosticsJsonWriter property)
        {
            this.scopes.Add(new CosmosDiagnosticScopeAttribute(name, property));
        }

        internal void AddStringAttribute(string name, string property)
        {
            this.scopes.Add(new CosmosDiagnosticScopeStringAttribute(name, property));
        }

        private void AppendJsonRetryInfo(StringBuilder stringBuilder)
        {
            stringBuilder.Append("\"RetryCount\":");
            stringBuilder.Append(this.retryCount);
            if (this.retryBackoffTimeSpan == null)
            {
                stringBuilder.Append(",\"RetryTimeDelay\":\"");

                stringBuilder.Append(this.retryBackoffTimeSpan);
                stringBuilder.Append("\"");
            }
        }

        private void AppendJsonUserAgentInfo(StringBuilder stringBuilder)
        {
            stringBuilder.Append("\"UserAgent\":\"");
            stringBuilder.Append(this.userAgent);
            stringBuilder.Append("\"");
        }

        /// <summary>
        /// Supports appending a single string value to the list
        /// </summary>
        private struct CosmosDiagnosticScopeStringAttribute : ICosmosDiagnosticsJsonWriter
        {
            private readonly string name;
            private readonly string jsonValue;

            internal CosmosDiagnosticScopeStringAttribute(
                string name,
                string jsonValue)
            {
                this.name = name;
                this.jsonValue = jsonValue;
            }

            public void AppendJson(StringBuilder stringBuilder)
            {
                stringBuilder.Append("\"");
                stringBuilder.Append(this.name);
                stringBuilder.Append("\":\"");
                stringBuilder.Append(this.jsonValue);
                stringBuilder.Append("\"");
            }
        }

        /// <summary>
        /// Supports appending an object that implements ICosmosDiagnosticsJsonWriter so
        /// it can be lazy.
        /// </summary>
        private struct CosmosDiagnosticScopeAttribute : ICosmosDiagnosticsJsonWriter
        {
            private readonly string name;
            private readonly ICosmosDiagnosticsJsonWriter jsonValue;

            internal CosmosDiagnosticScopeAttribute(
                string name,
                ICosmosDiagnosticsJsonWriter jsonValue)
            {
                this.name = name;
                this.jsonValue = jsonValue;
            }

            public void AppendJson(StringBuilder stringBuilder)
            {
                stringBuilder.Append("\"");
                stringBuilder.Append(this.name);
                stringBuilder.Append("\":");
                this.jsonValue.AppendJson(stringBuilder);
            }
        }

        /// <summary>
        /// This represents a single scope in the diagnostics.
        /// A scope is a section of code that is important to track.
        /// For example there is a scope for serialization, retry handlers, etc..
        /// </summary>
        internal struct CosmosDiagnosticsScope : IDisposable, ICosmosDiagnosticsJsonWriter
        {
            private string Name { get; }
            private DateTimeOffset StartTime { get; }
            private Stopwatch ElapsedTime { get; }
            
            internal CosmosDiagnosticsCore ParentDiagnosticsCore { get; }

            internal CosmosDiagnosticsScope(
                CosmosDiagnosticsCore cosmosDiagnosticsCore,
                string name)
            {
                this.ParentDiagnosticsCore = cosmosDiagnosticsCore;
                this.Name = name;
                this.StartTime = DateTimeOffset.UtcNow;
                this.ElapsedTime = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                this.ElapsedTime.Stop();
            }

            public void AppendJson(StringBuilder stringBuilder)
            {
                stringBuilder.Append("\"");
                stringBuilder.Append(this.Name);
                stringBuilder.Append("\": {\"StartTime\":\"");
                stringBuilder.Append(this.StartTime);
                stringBuilder.Append("\",\"ElapsedTime\":\"");
                stringBuilder.Append(this.ElapsedTime.Elapsed);
                stringBuilder.Append("\"}");
            }
        }
    }
}
