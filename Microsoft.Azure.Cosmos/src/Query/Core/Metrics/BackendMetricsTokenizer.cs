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
            DocumentLoadTimeInMs,
            WriteOutputTimeInMs,
            IndexUtilizationRatio,
            IndexLookupTimeInMs,
            QueryLogicalPlanBuildTimeInMs,
            OutputDocumentCount,
            OutputDocumentSize,
            QueryPhysicalPlanBuildTimeInMs,
            QueryCompileTimeInMs,
            QueryOptimizationTimeInMs,
            RetrievedDocumentCount,
            RetrievedDocumentSize,
            SystemFunctionExecuteTimeInMs,
            TotalExecutionTimeInMs,
            UserFunctionExecuteTimeInMs,
            VMExecutionTimeInMs,
            EqualsDelimiter,
            SemiColonDelimiter,
            NumberToken,
        }

        public static class TokenBuffers
        {
            public static readonly ReadOnlyMemory<byte> DocumentLoadTimeInMs = Encoding.UTF8.GetBytes("documentLoadTimeInMs");
            public static readonly ReadOnlyMemory<byte> WriteOutputTimeInMs = Encoding.UTF8.GetBytes("writeOutputTimeInMs");
            public static readonly ReadOnlyMemory<byte> IndexUtilizationRatio = Encoding.UTF8.GetBytes("indexUtilizationRatio");
            public static readonly ReadOnlyMemory<byte> IndexLookupTimeInMs = Encoding.UTF8.GetBytes("indexLookupTimeInMs");
            public static readonly ReadOnlyMemory<byte> QueryLogicalPlanBuildTimeInMs = Encoding.UTF8.GetBytes("queryLogicalPlanBuildTimeInMs");
            public static readonly ReadOnlyMemory<byte> OutputDocumentCount = Encoding.UTF8.GetBytes("outputDocumentCount");
            public static readonly ReadOnlyMemory<byte> OutputDocumentSize = Encoding.UTF8.GetBytes("outputDocumentSize");
            public static readonly ReadOnlyMemory<byte> QueryPhysicalPlanBuildTimeInMs = Encoding.UTF8.GetBytes("queryPhysicalPlanBuildTimeInMs");
            public static readonly ReadOnlyMemory<byte> QueryCompileTimeInMs = Encoding.UTF8.GetBytes("queryCompileTimeInMs");
            public static readonly ReadOnlyMemory<byte> QueryOptimizationTimeInMs = Encoding.UTF8.GetBytes("queryOptimizationTimeInMs");
            public static readonly ReadOnlyMemory<byte> RetrievedDocumentCount = Encoding.UTF8.GetBytes("retrievedDocumentCount");
            public static readonly ReadOnlyMemory<byte> RetrievedDocumentSize = Encoding.UTF8.GetBytes("retrievedDocumentSize");
            public static readonly ReadOnlyMemory<byte> SystemFunctionExecuteTimeInMs = Encoding.UTF8.GetBytes("systemFunctionExecuteTimeInMs");
            public static readonly ReadOnlyMemory<byte> TotalExecutionTimeInMs = Encoding.UTF8.GetBytes("totalExecutionTimeInMs");
            public static readonly ReadOnlyMemory<byte> UserFunctionExecuteTimeInMs = Encoding.UTF8.GetBytes("userFunctionExecuteTimeInMs");
            public static readonly ReadOnlyMemory<byte> VMExecutionTimeInMs = Encoding.UTF8.GetBytes("VMExecutionTimeInMs");
        }

        public static (TokenType? tokenType, ReadOnlyMemory<byte> buffer) Read(ReadOnlySpan<byte> corpus)
        {
            if (corpus[0] == '=')
            {
                return (TokenType.EqualsDelimiter, default);
            }
            else if (corpus[0] == ';')
            {
                return (TokenType.SemiColonDelimiter, default);
            }
            else if (char.IsDigit((char)corpus[0]))
            {
                return (TokenType.NumberToken, default);
            }
            else
            {
                // Check to see if it's a property name token
                int index = corpus.IndexOf((byte)'=');
                if (index == -1)
                {
                    return (default, default);
                }

                return GetTokenType(corpus.Slice(start: 0, length: index));
            }
        }

        private static (TokenType? tokenType, ReadOnlyMemory<byte> tokenBuffer) GetTokenType(ReadOnlySpan<byte> buffer)
        {
            return buffer.Length switch
            {
                20 => GetTokenTypeLength20(buffer),
                19 => GetTokenTypeLength19(buffer),
                21 => GetTokenTypeLength21(buffer),
                29 => GetTokenTypeLength29(buffer),
                18 => GetTokenTypeLength18(buffer),
                30 => GetTokenTypeLength30(buffer),
                25 => GetTokenTypeLength25(buffer),
                22 => GetTokenTypeLength22(buffer),
                27 => GetTokenTypeLength27(buffer),
                _ => (default, default),
            };
        }

        private static (TokenType? tokenType, ReadOnlyMemory<byte> tokenBuffer) GetTokenTypeLength20(ReadOnlySpan<byte> buffer)
        {
            if (buffer.SequenceEqual(TokenBuffers.DocumentLoadTimeInMs.Span))
            {
                return (TokenType.DocumentLoadTimeInMs, TokenBuffers.DocumentLoadTimeInMs);
            }

            if (buffer.SequenceEqual(TokenBuffers.QueryCompileTimeInMs.Span))
            {
                return (TokenType.QueryCompileTimeInMs, TokenBuffers.QueryCompileTimeInMs);
            }

            return (default, default);
        }

        private static (TokenType? tokenType, ReadOnlyMemory<byte> tokenBuffer) GetTokenTypeLength19(ReadOnlySpan<byte> buffer)
        {
            if (buffer.SequenceEqual(TokenBuffers.WriteOutputTimeInMs.Span))
            {
                return (TokenType.WriteOutputTimeInMs, TokenBuffers.WriteOutputTimeInMs);
            }

            if (buffer.SequenceEqual(TokenBuffers.IndexLookupTimeInMs.Span))
            {
                return (TokenType.IndexLookupTimeInMs, TokenBuffers.IndexLookupTimeInMs);
            }

            if (buffer.SequenceEqual(TokenBuffers.OutputDocumentCount.Span))
            {
                return (TokenType.OutputDocumentCount, TokenBuffers.OutputDocumentCount);
            }

            if (buffer.SequenceEqual(TokenBuffers.VMExecutionTimeInMs.Span))
            {
                return (TokenType.VMExecutionTimeInMs, TokenBuffers.VMExecutionTimeInMs);
            }

            return (default, default);
        }

        private static (TokenType? tokenType, ReadOnlyMemory<byte> tokenBuffer) GetTokenTypeLength21(ReadOnlySpan<byte> buffer)
        {
            if (buffer.SequenceEqual(TokenBuffers.IndexUtilizationRatio.Span))
            {
                return (TokenType.IndexUtilizationRatio, TokenBuffers.IndexUtilizationRatio);
            }

            if (buffer.SequenceEqual(TokenBuffers.RetrievedDocumentSize.Span))
            {
                return (TokenType.RetrievedDocumentSize, TokenBuffers.RetrievedDocumentSize);
            }

            return (default, default);
        }

        private static (TokenType? tokenType, ReadOnlyMemory<byte> tokenBuffer) GetTokenTypeLength29(ReadOnlySpan<byte> buffer)
        {
            if (buffer.SequenceEqual(TokenBuffers.QueryLogicalPlanBuildTimeInMs.Span))
            {
                return (TokenType.QueryLogicalPlanBuildTimeInMs, TokenBuffers.QueryLogicalPlanBuildTimeInMs);
            }

            if (buffer.SequenceEqual(TokenBuffers.SystemFunctionExecuteTimeInMs.Span))
            {
                return (TokenType.SystemFunctionExecuteTimeInMs, TokenBuffers.SystemFunctionExecuteTimeInMs);
            }

            return (default, default);
        }

        private static (TokenType? tokenType, ReadOnlyMemory<byte> tokenBuffer) GetTokenTypeLength18(ReadOnlySpan<byte> buffer)
        {
            if (buffer.SequenceEqual(TokenBuffers.OutputDocumentSize.Span))
            {
                return (TokenType.OutputDocumentSize, TokenBuffers.OutputDocumentSize);
            }

            return (default, default);
        }

        private static (TokenType? tokenType, ReadOnlyMemory<byte> tokenBuffer) GetTokenTypeLength30(ReadOnlySpan<byte> buffer)
        {
            if (buffer.SequenceEqual(TokenBuffers.QueryPhysicalPlanBuildTimeInMs.Span))
            {
                return (TokenType.QueryPhysicalPlanBuildTimeInMs, TokenBuffers.QueryPhysicalPlanBuildTimeInMs);
            }

            return (default, default);
        }

        private static (TokenType? tokenType, ReadOnlyMemory<byte> tokenBuffer) GetTokenTypeLength25(ReadOnlySpan<byte> buffer)
        {
            if (buffer.SequenceEqual(TokenBuffers.QueryOptimizationTimeInMs.Span))
            {
                return (TokenType.QueryOptimizationTimeInMs, TokenBuffers.QueryOptimizationTimeInMs);
            }

            return (default, default);
        }

        private static (TokenType? tokenType, ReadOnlyMemory<byte> tokenBuffer) GetTokenTypeLength22(ReadOnlySpan<byte> buffer)
        {
            if (buffer.SequenceEqual(TokenBuffers.RetrievedDocumentCount.Span))
            {
                return (TokenType.RetrievedDocumentCount, TokenBuffers.RetrievedDocumentCount);
            }

            if (buffer.SequenceEqual(TokenBuffers.TotalExecutionTimeInMs.Span))
            {
                return (TokenType.TotalExecutionTimeInMs, TokenBuffers.TotalExecutionTimeInMs);
            }

            return (default, default);
        }

        private static (TokenType? tokenType, ReadOnlyMemory<byte> tokenBuffer) GetTokenTypeLength27(ReadOnlySpan<byte> buffer)
        {
            if (buffer.SequenceEqual(TokenBuffers.UserFunctionExecuteTimeInMs.Span))
            {
                return (TokenType.UserFunctionExecuteTimeInMs, TokenBuffers.UserFunctionExecuteTimeInMs);
            }

            return (default, default);
        }
    }
}
