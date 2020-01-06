//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Text;

    internal static class QueryMetricsTokenizer
    {
        public enum Token
        {
            DocumentLoadTimeInMs,
            DocumentWriteTimeInMs,
            EqualDelimiter,
            IndexHitDocumentCount,
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
            Unknown,
            UserDefinedFunctionExecutionTimeInMs,
            VMExecutionTimeInMs,
        }

        public static class TokenBuffers
        {
            public static readonly ReadOnlyMemory<byte> DocumentLoadTimeInMs = Encoding.UTF8.GetBytes("documentLoadTimeInMs");
            public static readonly ReadOnlyMemory<byte> DocumentWriteTimeInMs = Encoding.UTF8.GetBytes("writeOutputTimeInMs");
            public static readonly ReadOnlyMemory<byte> IndexHitDocumentCount = Encoding.UTF8.GetBytes("indexHitDocumentCount");
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

        public static Token Read(ReadOnlySpan<byte> corpus)
        {
            Token token;
            if (corpus[0] == '=')
            {
                token = Token.EqualDelimiter;
            }
            else if (corpus[0] == ';')
            {
                token = Token.SemiColonDelimiter;
            }
            else if (char.IsDigit((char)corpus[0]))
            {
                token = Token.NumberValue;
            }
            else if (corpus.StartsWith(TokenBuffers.DocumentLoadTimeInMs.Span))
            {
                token = Token.DocumentLoadTimeInMs;
            }
            else if (corpus.StartsWith(TokenBuffers.DocumentWriteTimeInMs.Span))
            {
                token = Token.DocumentWriteTimeInMs;
            }
            else if (corpus.StartsWith(TokenBuffers.IndexHitDocumentCount.Span))
            {
                token = Token.IndexHitDocumentCount;
            }
            else if (corpus.StartsWith(TokenBuffers.IndexHitRatio.Span))
            {
                token = Token.IndexHitRatio;
            }
            else if (corpus.StartsWith(TokenBuffers.IndexLookupTimeInMs.Span))
            {
                token = Token.IndexLookupTimeInMs;
            }
            else if (corpus.StartsWith(TokenBuffers.LogicalPlanBuildTimeInMs.Span))
            {
                token = Token.LogicalPlanBuildTimeInMs;
            }
            else if (corpus.StartsWith(TokenBuffers.OutputDocumentCount.Span))
            {
                token = Token.OutputDocumentCount;
            }
            else if (corpus.StartsWith(TokenBuffers.OutputDocumentSize.Span))
            {
                token = Token.OutputDocumentSize;
            }
            else if (corpus.StartsWith(TokenBuffers.PhysicalPlanBuildTimeInMs.Span))
            {
                token = Token.PhysicalPlanBuildTimeInMs;
            }
            else if (corpus.StartsWith(TokenBuffers.QueryCompileTimeInMs.Span))
            {
                token = Token.QueryCompileTimeInMs;
            }
            else if (corpus.StartsWith(TokenBuffers.QueryOptimizationTimeInMs.Span))
            {
                token = Token.QueryOptimizationTimeInMs;
            }
            else if (corpus.StartsWith(TokenBuffers.RetrievedDocumentCount.Span))
            {
                token = Token.RetrievedDocumentCount;
            }
            else if (corpus.StartsWith(TokenBuffers.RetrievedDocumentSize.Span))
            {
                token = Token.RetrievedDocumentSize;
            }
            else if (corpus.StartsWith(TokenBuffers.SystemFunctionExecuteTimeInMs.Span))
            {
                token = Token.SystemFunctionExecuteTimeInMs;
            }
            else if (corpus.StartsWith(TokenBuffers.TotalQueryExecutionTimeInMs.Span))
            {
                token = Token.TotalQueryExecutionTimeInMs;
            }
            else if (corpus.StartsWith(TokenBuffers.UserDefinedFunctionExecutionTimeInMs.Span))
            {
                token = Token.UserDefinedFunctionExecutionTimeInMs;
            }
            else if (corpus.StartsWith(TokenBuffers.VMExecutionTimeInMs.Span))
            {
                token = Token.VMExecutionTimeInMs;
            }
            else
            {
                token = Token.Unknown;
            }

            return token;
        }
    }
}
