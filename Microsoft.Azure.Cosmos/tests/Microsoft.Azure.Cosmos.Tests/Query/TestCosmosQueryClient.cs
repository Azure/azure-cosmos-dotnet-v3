namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    internal class TestCosmosQueryClient : CosmosQueryClient
    {
        private readonly int targetRanges;

        public TestCosmosQueryClient(int targetRanges = 1)
        {
            this.targetRanges = targetRanges;
        }

        public override Action<IQueryable> OnExecuteScalarQueryCallback => throw new NotImplementedException();

        public override bool BypassQueryParsing()
        {
            return false;
        }

        public override void ClearSessionTokenCache(string collectionFullName)
        {
            throw new NotImplementedException();
        }

        public override Task<TryCatch<QueryPage>> ExecuteItemQueryAsync(string resourceUri, ResourceType resourceType, OperationType operationType, Cosmos.FeedRange feedRange, QueryRequestOptions requestOptions, AdditionalRequestHeaders additionalRequestHeaders, SqlQuerySpec sqlQuerySpec, string continuationToken, int pageSize, ITrace trace, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<PartitionedQueryExecutionInfo> ExecuteQueryPlanRequestAsync(string resourceUri, ResourceType resourceType, OperationType operationType, SqlQuerySpec sqlQuerySpec, Cosmos.PartitionKey? partitionKey, string supportedQueryFeatures, Guid clientQueryCorrelationId, ITrace trace, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PartitionedQueryExecutionInfo());
        }

        public override Task ForceRefreshCollectionCacheAsync(string collectionLink, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task<ContainerQueryProperties> GetCachedContainerQueryPropertiesAsync(string containerLink, Cosmos.PartitionKey? partitionKey, ITrace trace, CancellationToken cancellationToken)
        {
           return Task.FromResult(new ContainerQueryProperties(
                "test",
                new List<Range<string>>
                { 
                    new Range<string>(
                        PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                        PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                        true,
                        true)
                },
                new PartitionKeyDefinition(),
                Cosmos.GeospatialType.Geometry));
        }

        public override Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangeByFeedRangeAsync(string resourceLink, string collectionResourceId, PartitionKeyDefinition partitionKeyDefinition, FeedRangeInternal feedRangeInternal, bool forceRefresh, ITrace trace)
        {
            throw new NotImplementedException();
        }

        public override Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesAsync(string resourceLink, string collectionResourceId, IReadOnlyList<Range<string>> providedRanges, bool forceRefresh, ITrace trace)
        {
            List<PartitionKeyRange> partitionKeyRanges = new List<PartitionKeyRange>{new PartitionKeyRange()
                {
                    MinInclusive = PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                    MaxExclusive = PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey
                }};

            for (int i = 1; i < this.targetRanges; i++)
            {
                partitionKeyRanges.Add(new PartitionKeyRange() { MinInclusive = string.Empty, MaxExclusive = string.Empty });
            }
            return Task.FromResult(partitionKeyRanges);
        }

        public override Task<IReadOnlyList<PartitionKeyRange>> TryGetOverlappingRangesAsync(string collectionResourceId, Range<string> range, bool forceRefresh = false)
        {
            throw new NotImplementedException();
        }

        public override async Task<TryCatch<PartitionedQueryExecutionInfo>> TryGetPartitionedQueryExecutionInfoAsync(SqlQuerySpec sqlQuerySpec, ResourceType resourceType, PartitionKeyDefinition partitionKeyDefinition, bool requireFormattableOrderByQuery, bool isContinuationExpected, bool allowNonValueAggregateQuery, bool hasLogicalPartitionKey, bool allowDCount, bool useSystemPrefix, Cosmos.GeospatialType geospatialType, CancellationToken cancellationToken)
        {
            CosmosSerializerCore serializerCore = new();
            using StreamReader streamReader = new(serializerCore.ToStreamSqlQuerySpec(sqlQuerySpec, Documents.ResourceType.Document));
            string sqlQuerySpecJsonString = streamReader.ReadToEnd();

            TryCatch<PartitionedQueryExecutionInfo> queryPlan = OptimisticDirectExecutionQueryBaselineTests.TryGetPartitionedQueryExecutionInfo(sqlQuerySpecJsonString, partitionKeyDefinition);
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo = queryPlan.Succeeded ? queryPlan.Result : throw queryPlan.Exception;
            return TryCatch<PartitionedQueryExecutionInfo>.FromResult(partitionedQueryExecutionInfo);
        }
    }}
