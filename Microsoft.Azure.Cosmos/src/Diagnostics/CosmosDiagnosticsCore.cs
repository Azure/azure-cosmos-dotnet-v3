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
        private List<CosmosDiagnosticsScope> Scopes { get; } = new List<CosmosDiagnosticsScope>();
        private List<(string, dynamic)> jsonAttributes { get; } = new List<(string, dynamic)>();

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
            builder.Append("{\"Scopes\":[");
            if (this.Scopes != null && this.Scopes.Count > 0)
            {
                foreach (CosmosDiagnosticsScope scope in this.Scopes)
                {
                    scope.AppendJson(builder);
                    builder.Append(",");
                }
                // remove extra comma
                builder.Length -= 1;
            }

            builder.Append("]}");
            return builder.ToString();
        }

        internal void AddScope(CosmosDiagnosticsScope diagnosticsScope)
        {
            this.Scopes.Add(diagnosticsScope);
        }

        internal void AddJsonAttribute(string name, dynamic property)
        {
            this.jsonAttributes.Add((name, property));
        }

        private void AppendAttributes(StringBuilder stringBuilder)
        {
            if (this.jsonAttributes == null || this.jsonAttributes.Count == 0)
            {
                return;
            }
            foreach ((string name, dynamic value) attribute in this.jsonAttributes)
            {
                stringBuilder.Append($",\"{attribute.name}\":");
                stringBuilder.Append(attribute.value);
            }
        }

        internal struct CosmosDiagnosticsScope : IDisposable
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

            internal void AppendJson(StringBuilder builder)
            {
                builder.Append("{\"Name\":\"");
                builder.Append(this.Name);
                builder.Append("\",\"StartTime\":\"");
                builder.Append(this.StartTime);
                builder.Append("\",\"ElapsedTime\":\"");
                builder.Append(this.ElapsedTime.Elapsed);
                builder.Append("\"}");
            }
        }
    }
}
