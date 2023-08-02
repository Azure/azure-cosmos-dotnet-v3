//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Buffers.Text;
    using System.Text;

    /// <summary>
    /// Parser for <see cref="ServerSideMetrics"/>.
    /// </summary>
#if INTERNAL
#pragma warning disable SA1600
#pragma warning disable CS1591
    public
#else
    internal
#endif
    static class ServerSideMetricsParser
    {
        public static unsafe bool TryParse(string deliminatedString, out ServerSideMetrics serverSideMetrics)
        {
            if (deliminatedString == null)
            {
                throw new ArgumentNullException(nameof(deliminatedString));
            }

            if (deliminatedString.Length == 0)
            {
                // Stack allocating a zero length buffer returns a null pointer
                // so we special case the zero length string.
                serverSideMetrics = ServerSideMetrics.Empty;
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
                (ServerSideMetricsTokenizer.TokenType? tokenType, ReadOnlyMemory<byte> buffer) = ServerSideMetricsTokenizer.Read(corpus);
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
                        case ServerSideMetricsTokenizer.TokenType.DocumentLoadTimeInMs:
                            corpus = corpus.Slice(ServerSideMetricsTokenizer.TokenBuffers.DocumentLoadTimeInMs.Length);
                            if (!ServerSideMetricsParser.TryParseTimeSpanField(corpus, out documentLoadTime, out bytesConsumed))
                            {
                                serverSideMetrics = default;
                                return false;
                            }
                            break;

                        case ServerSideMetricsTokenizer.TokenType.WriteOutputTimeInMs:
                            corpus = corpus.Slice(ServerSideMetricsTokenizer.TokenBuffers.WriteOutputTimeInMs.Length);
                            if (!ServerSideMetricsParser.TryParseTimeSpanField(corpus, out documentWriteTime, out bytesConsumed))
                            {
                                serverSideMetrics = default;
                                return false;
                            }
                            break;

                        case ServerSideMetricsTokenizer.TokenType.IndexLookupTimeInMs:
                            corpus = corpus.Slice(ServerSideMetricsTokenizer.TokenBuffers.IndexLookupTimeInMs.Length);
                            if (!ServerSideMetricsParser.TryParseTimeSpanField(corpus, out indexLookupTime, out bytesConsumed))
                            {
                                serverSideMetrics = default;
                                return false;
                            }
                            break;

                        case ServerSideMetricsTokenizer.TokenType.IndexUtilizationRatio:
                            corpus = corpus.Slice(ServerSideMetricsTokenizer.TokenBuffers.IndexUtilizationRatio.Length);
                            if (!ServerSideMetricsParser.TryParseDoubleField(corpus, out indexHitRatio, out bytesConsumed))
                            {
                                serverSideMetrics = default;
                                return false;
                            }
                            break;

                        case ServerSideMetricsTokenizer.TokenType.QueryLogicalPlanBuildTimeInMs:
                            corpus = corpus.Slice(ServerSideMetricsTokenizer.TokenBuffers.QueryLogicalPlanBuildTimeInMs.Length);
                            if (!ServerSideMetricsParser.TryParseTimeSpanField(corpus, out logicalPlanBuildTime, out bytesConsumed))
                            {
                                serverSideMetrics = default;
                                return false;
                            }
                            break;

                        case ServerSideMetricsTokenizer.TokenType.OutputDocumentCount:
                            corpus = corpus.Slice(ServerSideMetricsTokenizer.TokenBuffers.OutputDocumentCount.Length);
                            if (!ServerSideMetricsParser.TryParseLongField(corpus, out outputDocumentCount, out bytesConsumed))
                            {
                                serverSideMetrics = default;
                                return false;
                            }
                            break;

                        case ServerSideMetricsTokenizer.TokenType.OutputDocumentSize:
                            corpus = corpus.Slice(ServerSideMetricsTokenizer.TokenBuffers.OutputDocumentSize.Length);
                            if (!ServerSideMetricsParser.TryParseLongField(corpus, out outputDocumentSize, out bytesConsumed))
                            {
                                serverSideMetrics = default;
                                return false;
                            }
                            break;

                        case ServerSideMetricsTokenizer.TokenType.QueryPhysicalPlanBuildTimeInMs:
                            corpus = corpus.Slice(ServerSideMetricsTokenizer.TokenBuffers.QueryPhysicalPlanBuildTimeInMs.Length);
                            if (!ServerSideMetricsParser.TryParseTimeSpanField(corpus, out physicalPlanBuildTime, out bytesConsumed))
                            {
                                serverSideMetrics = default;
                                return false;
                            }
                            break;

                        case ServerSideMetricsTokenizer.TokenType.QueryCompileTimeInMs:
                            corpus = corpus.Slice(ServerSideMetricsTokenizer.TokenBuffers.QueryCompileTimeInMs.Length);
                            if (!ServerSideMetricsParser.TryParseTimeSpanField(corpus, out queryCompilationTime, out bytesConsumed))
                            {
                                serverSideMetrics = default;
                                return false;
                            }
                            break;

                        case ServerSideMetricsTokenizer.TokenType.QueryOptimizationTimeInMs:
                            corpus = corpus.Slice(ServerSideMetricsTokenizer.TokenBuffers.QueryOptimizationTimeInMs.Length);
                            if (!ServerSideMetricsParser.TryParseTimeSpanField(corpus, out queryOptimizationTime, out bytesConsumed))
                            {
                                serverSideMetrics = default;
                                return false;
                            }
                            break;

                        case ServerSideMetricsTokenizer.TokenType.RetrievedDocumentCount:
                            corpus = corpus.Slice(ServerSideMetricsTokenizer.TokenBuffers.RetrievedDocumentCount.Length);
                            if (!ServerSideMetricsParser.TryParseLongField(corpus, out retrievedDocumentCount, out bytesConsumed))
                            {
                                serverSideMetrics = default;
                                return false;
                            }
                            break;

                        case ServerSideMetricsTokenizer.TokenType.RetrievedDocumentSize:
                            corpus = corpus.Slice(ServerSideMetricsTokenizer.TokenBuffers.RetrievedDocumentSize.Length);
                            if (!ServerSideMetricsParser.TryParseLongField(corpus, out retrievedDocumentSize, out bytesConsumed))
                            {
                                serverSideMetrics = default;
                                return false;
                            }
                            break;

                        case ServerSideMetricsTokenizer.TokenType.SystemFunctionExecuteTimeInMs:
                            corpus = corpus.Slice(ServerSideMetricsTokenizer.TokenBuffers.SystemFunctionExecuteTimeInMs.Length);
                            if (!ServerSideMetricsParser.TryParseTimeSpanField(corpus, out systemFunctionExecutionTime, out bytesConsumed))
                            {
                                serverSideMetrics = default;
                                return false;
                            }
                            break;

                        case ServerSideMetricsTokenizer.TokenType.TotalExecutionTimeInMs:
                            corpus = corpus.Slice(ServerSideMetricsTokenizer.TokenBuffers.TotalExecutionTimeInMs.Length);
                            if (!ServerSideMetricsParser.TryParseTimeSpanField(corpus, out totalQueryExecutionTime, out bytesConsumed))
                            {
                                serverSideMetrics = default;
                                return false;
                            }
                            break;

                        case ServerSideMetricsTokenizer.TokenType.UserFunctionExecuteTimeInMs:
                            corpus = corpus.Slice(ServerSideMetricsTokenizer.TokenBuffers.UserFunctionExecuteTimeInMs.Length);
                            if (!ServerSideMetricsParser.TryParseTimeSpanField(corpus, out userDefinedFunctionExecutionTime, out bytesConsumed))
                            {
                                serverSideMetrics = default;
                                return false;
                            }
                            break;

                        case ServerSideMetricsTokenizer.TokenType.VMExecutionTimeInMs:
                            corpus = corpus.Slice(ServerSideMetricsTokenizer.TokenBuffers.VMExecutionTimeInMs.Length);
                            if (!ServerSideMetricsParser.TryParseTimeSpanField(corpus, out vmExecutionTime, out bytesConsumed))
                            {
                                serverSideMetrics = default;
                                return false;
                            }
                            break;

                        default:
                            serverSideMetrics = default;
                            return false;
                    }
                }
                corpus = corpus.Slice(bytesConsumed);
                if (!corpus.IsEmpty)
                {
                    (ServerSideMetricsTokenizer.TokenType? semicolonToken, ReadOnlyMemory<byte> semicolonBuffer) = ServerSideMetricsTokenizer.Read(corpus);
                    if (!semicolonToken.HasValue || (semicolonToken != ServerSideMetricsTokenizer.TokenType.SemiColonDelimiter))
                    {
                        serverSideMetrics = default;
                        return false;
                    }

                    corpus = corpus.Slice(1);
                }
            }

            serverSideMetrics = new ServerSideMetrics(
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
            (ServerSideMetricsTokenizer.TokenType? tokenType, ReadOnlyMemory<byte> buffer) = ServerSideMetricsTokenizer.Read(corpus);
            if (!tokenType.HasValue || (tokenType.Value != ServerSideMetricsTokenizer.TokenType.EqualsDelimiter))
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
            (ServerSideMetricsTokenizer.TokenType? tokenType, ReadOnlyMemory<byte> buffer) = ServerSideMetricsTokenizer.Read(corpus);
            if (!tokenType.HasValue || (tokenType.Value != ServerSideMetricsTokenizer.TokenType.EqualsDelimiter))
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
            (ServerSideMetricsTokenizer.TokenType? tokenType, ReadOnlyMemory<byte> buffer) = ServerSideMetricsTokenizer.Read(corpus);
            if (!tokenType.HasValue || (tokenType.Value != ServerSideMetricsTokenizer.TokenType.EqualsDelimiter))
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
