//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Buffers.Text;
    using System.Text;

    /// <summary>
    /// Parser for <see cref="BackendMetrics"/>.
    /// </summary>
#if INTERNAL
#pragma warning disable SA1600
#pragma warning disable CS1591
    public
#else
    internal
#endif
    static class BackendMetricsParser
    {
        public static unsafe bool TryParse(string deliminatedString, out BackendMetrics backendMetrics)
        {
            if (deliminatedString == null)
            {
                throw new ArgumentNullException(nameof(deliminatedString));
            }

            if (deliminatedString.Length == 0)
            {
                // Stack allocating a zero length buffer returns a null pointer
                // so we special case the zero length string.
                backendMetrics = BackendMetrics.Empty;
                return true;
            }

            // QueryMetrics
            long retrievedDocumentCount = default;
            long retrievedDocumentSize = default;
            long outputDocumentCount = default;
            long outputDocumentSize = default;
            double indexHitRatio = default;
            TimeSpan totalQueryExecutionTime = default;

            // QueryPreparationTimes
            TimeSpan queryCompilationTime = default;
            TimeSpan logicalPlanBuildTime = default;
            TimeSpan physicalPlanBuildTime = default;
            TimeSpan queryOptimizationTime = default;

            // QueryTimes
            TimeSpan indexLookupTime = default;
            TimeSpan documentLoadTime = default;
            TimeSpan vmExecutionTime = default;
            TimeSpan documentWriteTime = default;

            // RuntimeExecutionTimes
            TimeSpan systemFunctionExecutionTime = default;
            TimeSpan userDefinedFunctionExecutionTime = default;

            const int MaxStackAlloc = 4 * 1024;
            int corpusLengthInBytes = deliminatedString.Length * 4;
            ReadOnlySpan<byte> corpus = (corpusLengthInBytes <= MaxStackAlloc) ? stackalloc byte[corpusLengthInBytes] : new byte[corpusLengthInBytes];
            fixed (char* deliminatedStringPointer = deliminatedString)
            {
                fixed (byte* corpusPointer = corpus)
                {
                    int bytesEncoded = Encoding.UTF8.GetBytes(deliminatedStringPointer, deliminatedString.Length, corpusPointer, corpus.Length);
                    corpus = corpus.Slice(0, bytesEncoded);
                }
            }

            while (!corpus.IsEmpty)
            {
                (BackendMetricsTokenizer.TokenType? tokenType, ReadOnlyMemory<byte> buffer) = BackendMetricsTokenizer.Read(corpus);
                int bytesConsumed;

                if (!tokenType.HasValue)
                {
                    // If the token is unknown, then just skip till the next field (';' or EOF)
                    // since the token must have been added recently in the service and the newer SDKs should know how to parse it
                    // this avoids breaking old clients
                    int nextTokenIndex = corpus.IndexOf((byte)';');
                    if (nextTokenIndex == -1)
                    {
                        // The next token does not exist, so just seek to the end
                        bytesConsumed = corpus.Length;
                    }
                    else
                    {
                        bytesConsumed = nextTokenIndex;
                    }
                }
                else
                {
                    switch (tokenType.Value)
                    {
                        case BackendMetricsTokenizer.TokenType.DocumentLoadTimeInMs:
                            corpus = corpus.Slice(BackendMetricsTokenizer.TokenBuffers.DocumentLoadTimeInMs.Length);
                            if (!BackendMetricsParser.TryParseTimeSpanField(corpus, out documentLoadTime, out bytesConsumed))
                            {
                                backendMetrics = default;
                                return false;
                            }
                            break;

                        case BackendMetricsTokenizer.TokenType.WriteOutputTimeInMs:
                            corpus = corpus.Slice(BackendMetricsTokenizer.TokenBuffers.WriteOutputTimeInMs.Length);
                            if (!BackendMetricsParser.TryParseTimeSpanField(corpus, out documentWriteTime, out bytesConsumed))
                            {
                                backendMetrics = default;
                                return false;
                            }
                            break;

                        case BackendMetricsTokenizer.TokenType.IndexLookupTimeInMs:
                            corpus = corpus.Slice(BackendMetricsTokenizer.TokenBuffers.IndexLookupTimeInMs.Length);
                            if (!BackendMetricsParser.TryParseTimeSpanField(corpus, out indexLookupTime, out bytesConsumed))
                            {
                                backendMetrics = default;
                                return false;
                            }
                            break;

                        case BackendMetricsTokenizer.TokenType.IndexUtilizationRatio:
                            corpus = corpus.Slice(BackendMetricsTokenizer.TokenBuffers.IndexUtilizationRatio.Length);
                            if (!BackendMetricsParser.TryParseDoubleField(corpus, out indexHitRatio, out bytesConsumed))
                            {
                                backendMetrics = default;
                                return false;
                            }
                            break;

                        case BackendMetricsTokenizer.TokenType.QueryLogicalPlanBuildTimeInMs:
                            corpus = corpus.Slice(BackendMetricsTokenizer.TokenBuffers.QueryLogicalPlanBuildTimeInMs.Length);
                            if (!BackendMetricsParser.TryParseTimeSpanField(corpus, out logicalPlanBuildTime, out bytesConsumed))
                            {
                                backendMetrics = default;
                                return false;
                            }
                            break;

                        case BackendMetricsTokenizer.TokenType.OutputDocumentCount:
                            corpus = corpus.Slice(BackendMetricsTokenizer.TokenBuffers.OutputDocumentCount.Length);
                            if (!BackendMetricsParser.TryParseLongField(corpus, out outputDocumentCount, out bytesConsumed))
                            {
                                backendMetrics = default;
                                return false;
                            }
                            break;

                        case BackendMetricsTokenizer.TokenType.OutputDocumentSize:
                            corpus = corpus.Slice(BackendMetricsTokenizer.TokenBuffers.OutputDocumentSize.Length);
                            if (!BackendMetricsParser.TryParseLongField(corpus, out outputDocumentSize, out bytesConsumed))
                            {
                                backendMetrics = default;
                                return false;
                            }
                            break;

                        case BackendMetricsTokenizer.TokenType.QueryPhysicalPlanBuildTimeInMs:
                            corpus = corpus.Slice(BackendMetricsTokenizer.TokenBuffers.QueryPhysicalPlanBuildTimeInMs.Length);
                            if (!BackendMetricsParser.TryParseTimeSpanField(corpus, out physicalPlanBuildTime, out bytesConsumed))
                            {
                                backendMetrics = default;
                                return false;
                            }
                            break;

                        case BackendMetricsTokenizer.TokenType.QueryCompileTimeInMs:
                            corpus = corpus.Slice(BackendMetricsTokenizer.TokenBuffers.QueryCompileTimeInMs.Length);
                            if (!BackendMetricsParser.TryParseTimeSpanField(corpus, out queryCompilationTime, out bytesConsumed))
                            {
                                backendMetrics = default;
                                return false;
                            }
                            break;

                        case BackendMetricsTokenizer.TokenType.QueryOptimizationTimeInMs:
                            corpus = corpus.Slice(BackendMetricsTokenizer.TokenBuffers.QueryOptimizationTimeInMs.Length);
                            if (!BackendMetricsParser.TryParseTimeSpanField(corpus, out queryOptimizationTime, out bytesConsumed))
                            {
                                backendMetrics = default;
                                return false;
                            }
                            break;

                        case BackendMetricsTokenizer.TokenType.RetrievedDocumentCount:
                            corpus = corpus.Slice(BackendMetricsTokenizer.TokenBuffers.RetrievedDocumentCount.Length);
                            if (!BackendMetricsParser.TryParseLongField(corpus, out retrievedDocumentCount, out bytesConsumed))
                            {
                                backendMetrics = default;
                                return false;
                            }
                            break;

                        case BackendMetricsTokenizer.TokenType.RetrievedDocumentSize:
                            corpus = corpus.Slice(BackendMetricsTokenizer.TokenBuffers.RetrievedDocumentSize.Length);
                            if (!BackendMetricsParser.TryParseLongField(corpus, out retrievedDocumentSize, out bytesConsumed))
                            {
                                backendMetrics = default;
                                return false;
                            }
                            break;

                        case BackendMetricsTokenizer.TokenType.SystemFunctionExecuteTimeInMs:
                            corpus = corpus.Slice(BackendMetricsTokenizer.TokenBuffers.SystemFunctionExecuteTimeInMs.Length);
                            if (!BackendMetricsParser.TryParseTimeSpanField(corpus, out systemFunctionExecutionTime, out bytesConsumed))
                            {
                                backendMetrics = default;
                                return false;
                            }
                            break;

                        case BackendMetricsTokenizer.TokenType.TotalExecutionTimeInMs:
                            corpus = corpus.Slice(BackendMetricsTokenizer.TokenBuffers.TotalExecutionTimeInMs.Length);
                            if (!BackendMetricsParser.TryParseTimeSpanField(corpus, out totalQueryExecutionTime, out bytesConsumed))
                            {
                                backendMetrics = default;
                                return false;
                            }
                            break;

                        case BackendMetricsTokenizer.TokenType.UserFunctionExecuteTimeInMs:
                            corpus = corpus.Slice(BackendMetricsTokenizer.TokenBuffers.UserFunctionExecuteTimeInMs.Length);
                            if (!BackendMetricsParser.TryParseTimeSpanField(corpus, out userDefinedFunctionExecutionTime, out bytesConsumed))
                            {
                                backendMetrics = default;
                                return false;
                            }
                            break;

                        case BackendMetricsTokenizer.TokenType.VMExecutionTimeInMs:
                            corpus = corpus.Slice(BackendMetricsTokenizer.TokenBuffers.VMExecutionTimeInMs.Length);
                            if (!BackendMetricsParser.TryParseTimeSpanField(corpus, out vmExecutionTime, out bytesConsumed))
                            {
                                backendMetrics = default;
                                return false;
                            }
                            break;

                        default:
                            backendMetrics = default;
                            return false;
                    }
                }
                corpus = corpus.Slice(bytesConsumed);
                if (!corpus.IsEmpty)
                {
                    (BackendMetricsTokenizer.TokenType? semicolonToken, ReadOnlyMemory<byte> semicolonBuffer) = BackendMetricsTokenizer.Read(corpus);
                    if (!semicolonToken.HasValue || (semicolonToken != BackendMetricsTokenizer.TokenType.SemiColonDelimiter))
                    {
                        backendMetrics = default;
                        return false;
                    }

                    corpus = corpus.Slice(1);
                }
            }

            backendMetrics = new BackendMetrics(
                retrievedDocumentCount: retrievedDocumentCount,
                retrievedDocumentSize: retrievedDocumentSize,
                outputDocumentCount: outputDocumentCount,
                outputDocumentSize: outputDocumentSize,
                indexHitRatio: indexHitRatio,
                totalQueryExecutionTime: totalQueryExecutionTime,
                queryPreparationTimes: new QueryPreparationTimes(
                    queryCompilationTime: queryCompilationTime,
                    logicalPlanBuildTime: logicalPlanBuildTime,
                    physicalPlanBuildTime: physicalPlanBuildTime,
                    queryOptimizationTime: queryOptimizationTime),
                indexLookupTime: indexLookupTime,
                documentLoadTime: documentLoadTime,
                vmExecutionTime: vmExecutionTime,
                runtimeExecutionTimes: new RuntimeExecutionTimes(
                    queryEngineExecutionTime: vmExecutionTime - indexLookupTime - documentLoadTime - documentWriteTime,
                    systemFunctionExecutionTime: systemFunctionExecutionTime,
                    userDefinedFunctionExecutionTime: userDefinedFunctionExecutionTime),
                documentWriteTime: documentWriteTime);
            return true;
        }

        private static bool TryParseTimeSpanField(ReadOnlySpan<byte> corpus, out TimeSpan timeSpan, out int bytesConsumed)
        {
            (BackendMetricsTokenizer.TokenType? tokenType, _) = BackendMetricsTokenizer.Read(corpus);
            if (!tokenType.HasValue || (tokenType.Value != BackendMetricsTokenizer.TokenType.EqualsDelimiter))
            {
                timeSpan = default;
                bytesConsumed = default;
                return false;
            }

            corpus = corpus.Slice(1);
            if (!Utf8Parser.TryParse(corpus, out double milliseconds, out bytesConsumed))
            {
                timeSpan = default;
                return false;
            }

            // Can not use TimeSpan.FromMilliseconds since double has a loss of precision
            timeSpan = TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * milliseconds));
            bytesConsumed++;
            return true;
        }

        private static bool TryParseLongField(ReadOnlySpan<byte> corpus, out long value, out int bytesConsumed)
        {
            (BackendMetricsTokenizer.TokenType? tokenType, _) = BackendMetricsTokenizer.Read(corpus);
            if (!tokenType.HasValue || (tokenType.Value != BackendMetricsTokenizer.TokenType.EqualsDelimiter))
            {
                value = default;
                bytesConsumed = default;
                return false;
            }

            corpus = corpus.Slice(1);
            if (!Utf8Parser.TryParse(corpus, out value, out bytesConsumed))
            {
                value = default;
                return false;
            }

            bytesConsumed++;
            return true;
        }

        private static bool TryParseDoubleField(ReadOnlySpan<byte> corpus, out double value, out int bytesConsumed)
        {
            (BackendMetricsTokenizer.TokenType? tokenType, _) = BackendMetricsTokenizer.Read(corpus);
            if (!tokenType.HasValue || (tokenType.Value != BackendMetricsTokenizer.TokenType.EqualsDelimiter))
            {
                value = default;
                bytesConsumed = default;
                return false;
            }

            corpus = corpus.Slice(1);
            if (!Utf8Parser.TryParse(corpus, out value, out bytesConsumed))
            {
                value = default;
                return false;
            }

            bytesConsumed++;
            return true;
        }
    }
}
