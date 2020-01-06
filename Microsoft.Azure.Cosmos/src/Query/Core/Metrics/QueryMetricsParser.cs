//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Buffers.Text;
    using System.Text;

    internal static class QueryMetricsParser
    {
        public static unsafe bool TryParse(string deliminatedString, out QueryMetrics queryMetrics)
        {
            if (deliminatedString == null)
            {
                throw new ArgumentNullException(nameof(deliminatedString));
            }

            // QueryMetrics
            long retrievedDocumentCount = default;
            long retrievedDocumentSize = default;
            long outputDocumentCount = default;
            long outputDocumentSize = default;
            long indexHitDocumentCount = default;
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
            int corpusLengthInBytes = deliminatedString.Length * 2;
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
                QueryMetricsTokenizer.Token token = QueryMetricsTokenizer.Read(corpus);
                int bytesConsumed;
                switch (token)
                {
                    case QueryMetricsTokenizer.Token.DocumentLoadTimeInMs:
                        corpus = corpus.Slice(QueryMetricsTokenizer.TokenBuffers.DocumentLoadTimeInMs.Length);
                        if (!QueryMetricsParser.TryParseTimeSpanField(corpus, out documentLoadTime, out bytesConsumed))
                        {
                            queryMetrics = default;
                            return false;
                        }
                        break;

                    case QueryMetricsTokenizer.Token.DocumentWriteTimeInMs:
                        corpus = corpus.Slice(QueryMetricsTokenizer.TokenBuffers.DocumentWriteTimeInMs.Length);
                        if (!QueryMetricsParser.TryParseTimeSpanField(corpus, out documentWriteTime, out bytesConsumed))
                        {
                            queryMetrics = default;
                            return false;
                        }
                        break;

                    case QueryMetricsTokenizer.Token.IndexHitDocumentCount:
                        corpus = corpus.Slice(QueryMetricsTokenizer.TokenBuffers.IndexHitDocumentCount.Length);
                        if (!QueryMetricsParser.TryParseLongField(corpus, out indexHitDocumentCount, out bytesConsumed))
                        {
                            queryMetrics = default;
                            return false;
                        }
                        break;

                    case QueryMetricsTokenizer.Token.IndexLookupTimeInMs:
                        corpus = corpus.Slice(QueryMetricsTokenizer.TokenBuffers.IndexLookupTimeInMs.Length);
                        if (!QueryMetricsParser.TryParseTimeSpanField(corpus, out indexLookupTime, out bytesConsumed))
                        {
                            queryMetrics = default;
                            return false;
                        }
                        break;

                    case QueryMetricsTokenizer.Token.LogicalPlanBuildTimeInMs:
                        corpus = corpus.Slice(QueryMetricsTokenizer.TokenBuffers.LogicalPlanBuildTimeInMs.Length);
                        if (!QueryMetricsParser.TryParseTimeSpanField(corpus, out logicalPlanBuildTime, out bytesConsumed))
                        {
                            queryMetrics = default;
                            return false;
                        }
                        break;

                    case QueryMetricsTokenizer.Token.OutputDocumentCount:
                        corpus = corpus.Slice(QueryMetricsTokenizer.TokenBuffers.OutputDocumentCount.Length);
                        if (!QueryMetricsParser.TryParseLongField(corpus, out outputDocumentCount, out bytesConsumed))
                        {
                            queryMetrics = default;
                            return false;
                        }
                        break;

                    case QueryMetricsTokenizer.Token.OutputDocumentSize:
                        corpus = corpus.Slice(QueryMetricsTokenizer.TokenBuffers.OutputDocumentSize.Length);
                        if (!QueryMetricsParser.TryParseLongField(corpus, out outputDocumentSize, out bytesConsumed))
                        {
                            queryMetrics = default;
                            return false;
                        }
                        break;

                    case QueryMetricsTokenizer.Token.PhysicalPlanBuildTimeInMs:
                        corpus = corpus.Slice(QueryMetricsTokenizer.TokenBuffers.PhysicalPlanBuildTimeInMs.Length);
                        if (!QueryMetricsParser.TryParseTimeSpanField(corpus, out physicalPlanBuildTime, out bytesConsumed))
                        {
                            queryMetrics = default;
                            return false;
                        }
                        break;

                    case QueryMetricsTokenizer.Token.QueryCompileTimeInMs:
                        corpus = corpus.Slice(QueryMetricsTokenizer.TokenBuffers.QueryCompileTimeInMs.Length);
                        if (!QueryMetricsParser.TryParseTimeSpanField(corpus, out queryCompilationTime, out bytesConsumed))
                        {
                            queryMetrics = default;
                            return false;
                        }
                        break;

                    case QueryMetricsTokenizer.Token.QueryOptimizationTimeInMs:
                        corpus = corpus.Slice(QueryMetricsTokenizer.TokenBuffers.QueryOptimizationTimeInMs.Length);
                        if (!QueryMetricsParser.TryParseTimeSpanField(corpus, out queryOptimizationTime, out bytesConsumed))
                        {
                            queryMetrics = default;
                            return false;
                        }
                        break;

                    case QueryMetricsTokenizer.Token.RetrievedDocumentCount:
                        corpus = corpus.Slice(QueryMetricsTokenizer.TokenBuffers.RetrievedDocumentCount.Length);
                        if (!QueryMetricsParser.TryParseLongField(corpus, out retrievedDocumentCount, out bytesConsumed))
                        {
                            queryMetrics = default;
                            return false;
                        }
                        break;

                    case QueryMetricsTokenizer.Token.RetrievedDocumentSize:
                        corpus = corpus.Slice(QueryMetricsTokenizer.TokenBuffers.RetrievedDocumentSize.Length);
                        if (!QueryMetricsParser.TryParseLongField(corpus, out retrievedDocumentSize, out bytesConsumed))
                        {
                            queryMetrics = default;
                            return false;
                        }
                        break;

                    case QueryMetricsTokenizer.Token.SystemFunctionExecuteTimeInMs:
                        corpus = corpus.Slice(QueryMetricsTokenizer.TokenBuffers.SystemFunctionExecuteTimeInMs.Length);
                        if (!QueryMetricsParser.TryParseTimeSpanField(corpus, out systemFunctionExecutionTime, out bytesConsumed))
                        {
                            queryMetrics = default;
                            return false;
                        }
                        break;

                    case QueryMetricsTokenizer.Token.TotalQueryExecutionTimeInMs:
                        corpus = corpus.Slice(QueryMetricsTokenizer.TokenBuffers.TotalQueryExecutionTimeInMs.Length);
                        if (!QueryMetricsParser.TryParseTimeSpanField(corpus, out totalQueryExecutionTime, out bytesConsumed))
                        {
                            queryMetrics = default;
                            return false;
                        }
                        break;

                    case QueryMetricsTokenizer.Token.UserDefinedFunctionExecutionTimeInMs:
                        corpus = corpus.Slice(QueryMetricsTokenizer.TokenBuffers.UserDefinedFunctionExecutionTimeInMs.Length);
                        if (!QueryMetricsParser.TryParseTimeSpanField(corpus, out userDefinedFunctionExecutionTime, out bytesConsumed))
                        {
                            queryMetrics = default;
                            return false;
                        }
                        break;

                    case QueryMetricsTokenizer.Token.VMExecutionTimeInMs:
                        corpus = corpus.Slice(QueryMetricsTokenizer.TokenBuffers.VMExecutionTimeInMs.Length);
                        if (!QueryMetricsParser.TryParseTimeSpanField(corpus, out vmExecutionTime, out bytesConsumed))
                        {
                            queryMetrics = default;
                            return false;
                        }
                        break;

                    default:
                        queryMetrics = default;
                        return false;
                }

                corpus = corpus.Slice(bytesConsumed);
                if (!corpus.IsEmpty)
                {
                    QueryMetricsTokenizer.Token semicolonToken = QueryMetricsTokenizer.Read(corpus);
                    if (semicolonToken != QueryMetricsTokenizer.Token.SemiColonDelimiter)
                    {
                        queryMetrics = default;
                        return false;
                    }

                    corpus = corpus.Slice(1);
                }
            }

            queryMetrics = new QueryMetrics(
                retrievedDocumentCount: retrievedDocumentCount,
                retrievedDocumentSize: retrievedDocumentSize,
                outputDocumentCount: outputDocumentCount,
                outputDocumentSize: outputDocumentSize,
                indexHitDocumentCount: indexHitDocumentCount,
                indexUtilizationInfo: IndexUtilizationInfo.Empty,
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
                documentWriteTime: documentWriteTime,
                clientSideMetrics: ClientSideMetrics.Zero);
            return true;
        }

        private static bool TryParseTimeSpanField(ReadOnlySpan<byte> corpus, out TimeSpan timeSpan, out int bytesConsumed)
        {
            QueryMetricsTokenizer.Token equalsDelimiterToken = QueryMetricsTokenizer.Read(corpus);
            if (equalsDelimiterToken != QueryMetricsTokenizer.Token.EqualDelimiter)
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
            QueryMetricsTokenizer.Token equalsDelimiterToken = QueryMetricsTokenizer.Read(corpus);
            if (equalsDelimiterToken != QueryMetricsTokenizer.Token.EqualDelimiter)
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
