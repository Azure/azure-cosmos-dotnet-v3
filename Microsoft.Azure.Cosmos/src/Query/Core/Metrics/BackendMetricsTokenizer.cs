//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Text;

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
            else if (corpus.StartsWith(TokenBuffers.DocumentLoadTimeInMs.Span))
            {
                token = TokenType.DocumentLoadTimeInMs;
            }
            else if (corpus.StartsWith(TokenBuffers.DocumentWriteTimeInMs.Span))
            {
                token = TokenType.DocumentWriteTimeInMs;
            }
            else if (corpus.StartsWith(TokenBuffers.IndexHitRatio.Span))
            {
                token = TokenType.IndexHitRatio;
            }
            else if (corpus.StartsWith(TokenBuffers.IndexLookupTimeInMs.Span))
            {
                token = TokenType.IndexLookupTimeInMs;
            }
            else if (corpus.StartsWith(TokenBuffers.LogicalPlanBuildTimeInMs.Span))
            {
                token = TokenType.LogicalPlanBuildTimeInMs;
            }
            else if (corpus.StartsWith(TokenBuffers.OutputDocumentCount.Span))
            {
                token = TokenType.OutputDocumentCount;
            }
            else if (corpus.StartsWith(TokenBuffers.OutputDocumentSize.Span))
            {
                token = TokenType.OutputDocumentSize;
            }
            else if (corpus.StartsWith(TokenBuffers.PhysicalPlanBuildTimeInMs.Span))
            {
                token = TokenType.PhysicalPlanBuildTimeInMs;
            }
            else if (corpus.StartsWith(TokenBuffers.QueryCompileTimeInMs.Span))
            {
                token = TokenType.QueryCompileTimeInMs;
            }
            else if (corpus.StartsWith(TokenBuffers.QueryOptimizationTimeInMs.Span))
            {
                token = TokenType.QueryOptimizationTimeInMs;
            }
            else if (corpus.StartsWith(TokenBuffers.RetrievedDocumentCount.Span))
            {
                token = TokenType.RetrievedDocumentCount;
            }
            else if (corpus.StartsWith(TokenBuffers.RetrievedDocumentSize.Span))
            {
                token = TokenType.RetrievedDocumentSize;
            }
            else if (corpus.StartsWith(TokenBuffers.SystemFunctionExecuteTimeInMs.Span))
            {
                token = TokenType.SystemFunctionExecuteTimeInMs;
            }
            else if (corpus.StartsWith(TokenBuffers.TotalQueryExecutionTimeInMs.Span))
            {
                token = TokenType.TotalQueryExecutionTimeInMs;
            }
            else if (corpus.StartsWith(TokenBuffers.UserDefinedFunctionExecutionTimeInMs.Span))
            {
                token = TokenType.UserDefinedFunctionExecutionTimeInMs;
            }
            else if (corpus.StartsWith(TokenBuffers.VMExecutionTimeInMs.Span))
            {
                token = TokenType.VMExecutionTimeInMs;
            }
            else
            {
                token = TokenType.Unknown;
            }

            return token;
        }
    }
}
