//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Query.ExecutionComponent;
    using Microsoft.Azure.Cosmos.Query.ParallelQuery;

    internal sealed class PipelinedDocumentQueryExecutionContext : IDocumentQueryExecutionContext
    {
        private readonly IDocumentQueryExecutionComponent component;
        private readonly int actualPageSize;
        private readonly Guid correlatedActivityId;

        private readonly SchedulingStopwatch executeNextSchedulingMetrics;
        
        private PipelinedDocumentQueryExecutionContext(
            IDocumentQueryExecutionComponent component,
            int actualPageSize,
            Guid correlatedActivityId)
        {
            this.component = component;
            this.actualPageSize = actualPageSize;
            this.correlatedActivityId = correlatedActivityId;

            this.executeNextSchedulingMetrics = new SchedulingStopwatch();
            this.executeNextSchedulingMetrics.Ready();

            DefaultTrace.TraceVerbose(string.Format(
                CultureInfo.InvariantCulture,
                "{0} Pipelined~Context, actual page size: {1}",
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                this.actualPageSize));
        }

        public static async Task<PipelinedDocumentQueryExecutionContext> CreateAsync(
            IDocumentQueryClient client,
            ResourceType resourceTypeEnum,
            Type resourceType,
            Expression expression,
            FeedOptions feedOptions,
            string resourceLink,
            string collectionRid,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            List<PartitionKeyRange> targetRanges,
            int initialPageSize,
            bool isContinuationExpected,
            bool getLazyFeedResponse,
            CancellationToken token,
            Guid correlatedActivityId)
        {
            DefaultTrace.TraceInformation(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}, CorrelatedActivityId: {1} | Pipelined~Context.CreateAsync",
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    correlatedActivityId));
            Func<string, Task<IDocumentQueryExecutionComponent>> createComponentFunc;

            QueryInfo queryInfo = partitionedQueryExecutionInfo.QueryInfo;

            if (queryInfo.HasOrderBy)
            {
                createComponentFunc = async (requestContinuation) =>
                {
                    return await OrderByDocumentQueryExecutionContext.CreateAsync(
                        client,
                        resourceTypeEnum,
                        resourceType,
                        expression,
                        feedOptions,
                        resourceLink,
                        collectionRid,
                        partitionedQueryExecutionInfo,
                        targetRanges,
                        initialPageSize,
                        isContinuationExpected,
                        getLazyFeedResponse,
                        requestContinuation,
                        token,
                        correlatedActivityId);
                };
            }
            else
            {
                createComponentFunc = async (requestContinuation) =>
                {
                    return await ParallelDocumentQueryExecutionContext.CreateAsync(
                        client,
                        resourceTypeEnum,
                        resourceType,
                        expression,
                        feedOptions,
                        resourceLink,
                        collectionRid,
                        partitionedQueryExecutionInfo,
                        targetRanges,
                        initialPageSize,
                        isContinuationExpected,
                        getLazyFeedResponse,
                        requestContinuation,
                        token,
                        correlatedActivityId);
                };
            }

            if (queryInfo.HasAggregates)
            {
                Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback = createComponentFunc;
                createComponentFunc = async (requestContinuation) =>
                {
                    return await AggregateDocumentQueryExecutionComponent.CreateAsync(
                        queryInfo.Aggregates,
                        requestContinuation,
                        createSourceCallback);
                };
            }

            if (queryInfo.HasDistinct)
            {
                Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback = createComponentFunc;
                createComponentFunc = async (requestContinuation) =>
                {
                    return await DistinctDocumentQueryExecutionComponent.CreateAsync(
                        requestContinuation,
                        createSourceCallback,
                        queryInfo.DistinctType);
                };
            }

            if (queryInfo.HasTop)
            {
                Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback = createComponentFunc;
                createComponentFunc = async (requestContinuation) =>
                {
                    return await TopDocumentQueryExecutionComponent.CreateAsync(
                        queryInfo.Top.Value,
                        requestContinuation,
                        createSourceCallback);
                };
            }

            int actualPageSize = feedOptions.MaxItemCount.GetValueOrDefault(ParallelQueryConfig.GetConfig().ClientInternalPageSize);

            // If this contract changes, make the corresponding change in MongoDocumentClient.QueryInternalAsync
            if (actualPageSize == -1)
            {
                actualPageSize = int.MaxValue;
            }

            return new PipelinedDocumentQueryExecutionContext(
                await createComponentFunc(feedOptions.RequestContinuation),
                Math.Min(actualPageSize, queryInfo.Top.GetValueOrDefault(actualPageSize)),
                correlatedActivityId);
        }

        public bool IsDone
        {
            get
            {
                return this.component.IsDone;
            }
        }

        public void Dispose()
        {
            this.component.Dispose();
        }

        public async Task<FeedResponse<dynamic>> ExecuteNextAsync(CancellationToken token)
        {
            this.executeNextSchedulingMetrics.Start();
            try
            {
                FeedResponse<dynamic> response = await this.component.DrainAsync(this.actualPageSize, token);
                return new FeedResponse<dynamic>(
                    response, 
                    response.Count, 
                    response.Headers, 
                    response.UseETagAsContinuation, 
                    this.component.GetQueryMetrics(),
                    response.RequestStatistics,
                    response.DisallowContinuationTokenMessage,
                    response.ResponseLengthBytes);
            }
            catch (Exception)
            {
                this.component.Stop();
                throw;
            }
            finally
            {
                this.executeNextSchedulingMetrics.Stop();
                if (this.IsDone)
                {
                    DefaultTrace.TraceInformation(
                        string.Format(
                        CultureInfo.InvariantCulture,
                            "{0}, CorrelatedActivityId {1} | Pipelined~Context Finished with CorrelatedActivityId: {1}",
                            DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                            this.component,
                            this.correlatedActivityId));
                }
            }
        }
    }
}
