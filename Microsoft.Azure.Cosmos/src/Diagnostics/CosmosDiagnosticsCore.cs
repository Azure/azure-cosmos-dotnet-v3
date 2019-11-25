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
    ///  Contains the cosmos diagnostic information for the current request to Azure Cosmos DB service.
    /// </summary>
    internal class CosmosDiagnosticsCore : CosmosDiagnostics
    {
        private List<ICosmosDiagnosticsJsonWriter> Scopes { get; } = new List<ICosmosDiagnosticsJsonWriter>();
        private long retryCount = 0;
        private TimeSpan retryBackoffTimeSpan;

        internal CosmosDiagnosticsScope CreateScope(string name)
        {
            CosmosDiagnosticsScope scope = new CosmosDiagnosticsScope(this, name);
            this.Scopes.Add(scope);
            return scope;
        }

        internal void JoinScopes(CosmosDiagnosticsCore leafScope)
        {
            this.Scopes.AddRange(leafScope.Scopes);
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("{");
            this.AppendJsonRetryInfo(builder);
            if (this.Scopes != null && this.Scopes.Count > 0)
            {
                foreach (ICosmosDiagnosticsJsonWriter scope in this.Scopes)
                {
                    builder.Append(",");
                    scope.AppendJson(builder); 
                }
            }
            builder.Append("}");

            return builder.ToString();
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
            this.Scopes.Add(diagnosticsScope);
        }

        internal void AddJsonAttribute(string name, dynamic property)
        {
            this.Scopes.Add(new CosmosDiagnosticScopeAttribute(name, property));
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

        private interface ICosmosDiagnosticsJsonWriter
        {
            void AppendJson(StringBuilder stringBuilder);
        }

        private struct CosmosDiagnosticScopeAttribute : ICosmosDiagnosticsJsonWriter
        {
            private readonly string name;
            private readonly dynamic jsonValue;

            internal CosmosDiagnosticScopeAttribute(
                string name,
                dynamic jsonValue)
            {
                this.name = name;
                this.jsonValue = jsonValue;
            }

            public void AppendJson(StringBuilder stringBuilder)
            {
                stringBuilder.Append("\"");
                stringBuilder.Append(this.name);
                stringBuilder.Append("\":");
                stringBuilder.Append(this.jsonValue);
            }
        }

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
                this.ElapsedTime = new Stopwatch();
                this.ElapsedTime.Start();
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
