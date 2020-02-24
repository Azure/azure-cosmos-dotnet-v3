//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    internal sealed class QueryPipelineDiagnostics : CosmosDiagnosticsInternal
    {
        public QueryPipelineDiagnostics(
            IReadOnlyCollection<CosmosDiagnosticScope> queryPipelineCreationScopes,
            CosmosDiagnosticsContext queryPlanFromGatewayDiagnostics)
        {
            this.QueryPipelineCreationScopes = queryPipelineCreationScopes ?? throw new ArgumentNullException(nameof(queryPipelineCreationScopes));
            this.QueryPlanFromGatewayDiagnostics = queryPlanFromGatewayDiagnostics;
        }

        internal IReadOnlyCollection<CosmosDiagnosticScope> QueryPipelineCreationScopes { get; }
        internal CosmosDiagnosticsContext QueryPlanFromGatewayDiagnostics { get; }

        internal static QueryPipelineDiagnostics Merge(
            QueryPipelineDiagnostics source,
            QueryPipelineDiagnostics target)
        {
            if (source == null)
            {
                return target;
            }

            if (target == null)
            {
                return source;
            }

            IReadOnlyCollection<CosmosDiagnosticScope> merged = source.QueryPipelineCreationScopes.Concat(target.QueryPipelineCreationScopes).ToList().AsReadOnly();

            CosmosDiagnosticsContext diagnosticsContext = source.QueryPlanFromGatewayDiagnostics;
            if (diagnosticsContext != null && target.QueryPlanFromGatewayDiagnostics != null)
            {
                diagnosticsContext.AddDiagnosticsInternal(target.QueryPlanFromGatewayDiagnostics);
            }
            else if (diagnosticsContext == null && target.QueryPlanFromGatewayDiagnostics != null)
            {
                diagnosticsContext = target.QueryPlanFromGatewayDiagnostics;
            }

            return new QueryPipelineDiagnostics(
                merged,
                diagnosticsContext);
        }

        public override void Accept(CosmosDiagnosticsInternalVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(CosmosDiagnosticsInternalVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        internal struct QueryPipelineDiagnosticBuilder
        {
            private readonly IList<CosmosDiagnosticScope> pipelineDiagnostics;
            private CosmosDiagnosticsContext gatewayDiagnosticContext;

            internal QueryPipelineDiagnosticBuilder(int capacity)
            {
                this.pipelineDiagnostics = new List<CosmosDiagnosticScope>(capacity);
                this.gatewayDiagnosticContext = null;
            }

            internal IDisposable CreateScope(string name)
            {
                if (this.pipelineDiagnostics == null)
                {
                    throw new ArgumentNullException(nameof(this.pipelineDiagnostics));
                }

                CosmosDiagnosticScope diagnosticScope = new CosmosDiagnosticScope(name);
                this.pipelineDiagnostics.Add(diagnosticScope);
                return diagnosticScope;
            }

            internal void AddGatewayDiagnostics(CosmosDiagnosticsContext gatewayDiagnosticContext)
            {
                if (this.gatewayDiagnosticContext == null)
                {
                    this.gatewayDiagnosticContext = gatewayDiagnosticContext;
                }
                else
                {
                    this.gatewayDiagnosticContext.AddDiagnosticsInternal(gatewayDiagnosticContext);
                }
            }

            internal QueryPipelineDiagnostics Build()
            {
                return new QueryPipelineDiagnostics(
                    new ReadOnlyCollection<CosmosDiagnosticScope>(this.pipelineDiagnostics),
                    this.gatewayDiagnosticContext);
            }
        }
    }
}
