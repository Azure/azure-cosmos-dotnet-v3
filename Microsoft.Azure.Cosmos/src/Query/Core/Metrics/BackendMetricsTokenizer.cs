//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Collections;

    /// <summary>
    /// Tokenizer for <see cref="BackendMetrics"/>
    /// </summary>
#if INTERNAL
#pragma warning disable SA1600
#pragma warning disable CS1591
#pragma warning disable SA1602 // Enumeration items should be documented
    public
#else
    internal
#endif
    static class BackendMetricsTokenizer
    {
        private static readonly Trie<byte, TokenType> PropertyNameTokens;

        static BackendMetricsTokenizer()
        {
            BackendMetricsTokenizer.PropertyNameTokens = new Trie<byte, TokenType>(initialCapacity: 32);
            BackendMetricsTokenizer.PropertyNameTokens.AddOrUpdate(TokenBuffers.DocumentLoadTimeInMs.Span, TokenType.DocumentLoadTimeInMs);
            BackendMetricsTokenizer.PropertyNameTokens.AddOrUpdate(TokenBuffers.DocumentWriteTimeInMs.Span, TokenType.DocumentWriteTimeInMs);
            BackendMetricsTokenizer.PropertyNameTokens.AddOrUpdate(TokenBuffers.IndexHitRatio.Span, TokenType.IndexHitRatio);
            BackendMetricsTokenizer.PropertyNameTokens.AddOrUpdate(TokenBuffers.IndexLookupTimeInMs.Span, TokenType.IndexLookupTimeInMs);
            BackendMetricsTokenizer.PropertyNameTokens.AddOrUpdate(TokenBuffers.LogicalPlanBuildTimeInMs.Span, TokenType.LogicalPlanBuildTimeInMs);
            BackendMetricsTokenizer.PropertyNameTokens.AddOrUpdate(TokenBuffers.OutputDocumentCount.Span, TokenType.OutputDocumentCount);
            BackendMetricsTokenizer.PropertyNameTokens.AddOrUpdate(TokenBuffers.OutputDocumentSize.Span, TokenType.OutputDocumentSize);
            BackendMetricsTokenizer.PropertyNameTokens.AddOrUpdate(TokenBuffers.PhysicalPlanBuildTimeInMs.Span, TokenType.PhysicalPlanBuildTimeInMs);
            BackendMetricsTokenizer.PropertyNameTokens.AddOrUpdate(TokenBuffers.QueryCompileTimeInMs.Span, TokenType.QueryCompileTimeInMs);
            BackendMetricsTokenizer.PropertyNameTokens.AddOrUpdate(TokenBuffers.QueryOptimizationTimeInMs.Span, TokenType.QueryOptimizationTimeInMs);
            BackendMetricsTokenizer.PropertyNameTokens.AddOrUpdate(TokenBuffers.RetrievedDocumentCount.Span, TokenType.RetrievedDocumentCount);
            BackendMetricsTokenizer.PropertyNameTokens.AddOrUpdate(TokenBuffers.RetrievedDocumentSize.Span, TokenType.RetrievedDocumentSize);
            BackendMetricsTokenizer.PropertyNameTokens.AddOrUpdate(TokenBuffers.SystemFunctionExecuteTimeInMs.Span, TokenType.SystemFunctionExecuteTimeInMs);
            BackendMetricsTokenizer.PropertyNameTokens.AddOrUpdate(TokenBuffers.TotalQueryExecutionTimeInMs.Span, TokenType.TotalQueryExecutionTimeInMs);
            BackendMetricsTokenizer.PropertyNameTokens.AddOrUpdate(TokenBuffers.UserDefinedFunctionExecutionTimeInMs.Span, TokenType.UserDefinedFunctionExecutionTimeInMs);
            BackendMetricsTokenizer.PropertyNameTokens.AddOrUpdate(TokenBuffers.VMExecutionTimeInMs.Span, TokenType.VMExecutionTimeInMs);
        }

        public enum TokenType
        {
            Unknown,
            DocumentLoadTimeInMs,
            DocumentWriteTimeInMs,
            EqualDelimiter,
            IndexHitRatio,
            IndexLookupTimeInMs,
            LogicalPlanBuildTimeInMs,
            NumberValue,
            OutputDocumentCount,
            OutputDocumentSize,
            PhysicalPlanBuildTimeInMs,
            QueryCompileTimeInMs,
            QueryOptimizationTimeInMs,
            RetrievedDocumentCount,
            RetrievedDocumentSize,
            SemiColonDelimiter,
            SystemFunctionExecuteTimeInMs,
            TotalQueryExecutionTimeInMs,
            UserDefinedFunctionExecutionTimeInMs,
            VMExecutionTimeInMs,
        }

        public static class TokenBuffers
        {
            public static readonly ReadOnlyMemory<byte> DocumentLoadTimeInMs = Encoding.UTF8.GetBytes("documentLoadTimeInMs");
            public static readonly ReadOnlyMemory<byte> DocumentWriteTimeInMs = Encoding.UTF8.GetBytes("writeOutputTimeInMs");
            public static readonly ReadOnlyMemory<byte> IndexHitRatio = Encoding.UTF8.GetBytes("indexUtilizationRatio");
            public static readonly ReadOnlyMemory<byte> IndexLookupTimeInMs = Encoding.UTF8.GetBytes("indexLookupTimeInMs");
            public static readonly ReadOnlyMemory<byte> LogicalPlanBuildTimeInMs = Encoding.UTF8.GetBytes("queryLogicalPlanBuildTimeInMs");
            public static readonly ReadOnlyMemory<byte> OutputDocumentCount = Encoding.UTF8.GetBytes("outputDocumentCount");
            public static readonly ReadOnlyMemory<byte> OutputDocumentSize = Encoding.UTF8.GetBytes("outputDocumentSize");
            public static readonly ReadOnlyMemory<byte> PhysicalPlanBuildTimeInMs = Encoding.UTF8.GetBytes("queryPhysicalPlanBuildTimeInMs");
            public static readonly ReadOnlyMemory<byte> QueryCompileTimeInMs = Encoding.UTF8.GetBytes("queryCompileTimeInMs");
            public static readonly ReadOnlyMemory<byte> QueryOptimizationTimeInMs = Encoding.UTF8.GetBytes("queryOptimizationTimeInMs");
            public static readonly ReadOnlyMemory<byte> RetrievedDocumentCount = Encoding.UTF8.GetBytes("retrievedDocumentCount");
            public static readonly ReadOnlyMemory<byte> RetrievedDocumentSize = Encoding.UTF8.GetBytes("retrievedDocumentSize");
            public static readonly ReadOnlyMemory<byte> SystemFunctionExecuteTimeInMs = Encoding.UTF8.GetBytes("systemFunctionExecuteTimeInMs");
            public static readonly ReadOnlyMemory<byte> TotalQueryExecutionTimeInMs = Encoding.UTF8.GetBytes("totalExecutionTimeInMs");
            public static readonly ReadOnlyMemory<byte> UserDefinedFunctionExecutionTimeInMs = Encoding.UTF8.GetBytes("userFunctionExecuteTimeInMs");
            public static readonly ReadOnlyMemory<byte> VMExecutionTimeInMs = Encoding.UTF8.GetBytes("VMExecutionTimeInMs");
        }

        public static TokenType Read(ReadOnlySpan<byte> corpus)
        {
            // This can be converted to a fancy prefix tree switch case in the future if we need better perf.

            TokenType token;
            if (corpus[0] == '=')
            {
                token = TokenType.EqualDelimiter;
            }
            else if (corpus[0] == ';')
            {
                token = TokenType.SemiColonDelimiter;
            }
            else if (char.IsDigit((char)corpus[0]))
            {
                token = TokenType.NumberValue;
            }
            else
            {
                // Check to see if it's a property name token
                int index = corpus.IndexOf((byte)'=');
                if (index == -1)
                {
                    return TokenType.Unknown;
                }

                if (!BackendMetricsTokenizer.PropertyNameTokens.TryGetValue(corpus.Slice(start: 0, length: index), out token))
                {
                    return TokenType.Unknown;
                }
            }

            return token;
        }
    }
}
