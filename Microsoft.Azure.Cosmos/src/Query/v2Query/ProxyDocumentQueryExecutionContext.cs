//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// This class is used as a proxy to wrap the DefaultDocumentQueryExecutionContext which is needed 
    /// for sending the query to Gateway first and then uses PipelinedDocumentQueryExecutionContext after
    /// it gets the necessary info. This has been added since we
    /// haven't produced Linux/Mac version of the ServiceInterop native binary which holds the logic for
    /// parsing the query without having this extra hop to Gateway
    /// </summary>
    internal sealed class ProxyDocumentQueryExecutionContext : IDocumentQueryExecutionContext
    {
        private readonly IDocumentQueryClient client;
        private readonly ResourceType resourceTypeEnum;
        private readonly Type resourceType;
        private readonly Expression expression;
        private readonly FeedOptions feedOptions;
        private readonly string resourceLink;

        private readonly ContainerProperties collection;
        private readonly bool isContinuationExpected;

        private readonly Guid correlatedActivityId;

        private IDocumentQueryExecutionContext innerExecutionContext;

        private ProxyDocumentQueryExecutionContext(
            IDocumentQueryExecutionContext innerExecutionContext,
            IDocumentQueryClient client,
            ResourceType resourceTypeEnum,
            Type resourceType,
            Expression expression,
            FeedOptions feedOptions,
            string resourceLink,
            ContainerProperties collection,
            bool isContinuationExpected,
            Guid correlatedActivityId)
        {
            this.innerExecutionContext = innerExecutionContext;

            this.client = client;
            this.resourceTypeEnum = resourceTypeEnum;
            this.resourceType = resourceType;
            this.expression = expression;
            this.feedOptions = feedOptions;
            this.resourceLink = resourceLink;

            this.collection = collection;
            this.isContinuationExpected = isContinuationExpected;

            this.correlatedActivityId = correlatedActivityId;
        }

        public static ProxyDocumentQueryExecutionContext Create(
            IDocumentQueryClient client,
            ResourceType resourceTypeEnum,
            Type resourceType,
            Expression expression,
            FeedOptions feedOptions,
            string resourceLink,
            CancellationToken token,
            ContainerProperties collection,
            bool isContinuationExpected,
            Guid correlatedActivityId)
        {
            token.ThrowIfCancellationRequested();
            DocumentQueryExecutionContextBase.InitParams constructorParams = new DocumentQueryExecutionContextBase.InitParams(client, resourceTypeEnum, resourceType, expression, feedOptions, resourceLink, false, correlatedActivityId);
            IDocumentQueryExecutionContext innerExecutionContext =
             new DefaultDocumentQueryExecutionContext(constructorParams, isContinuationExpected);

            return new ProxyDocumentQueryExecutionContext(innerExecutionContext, client,
                resourceTypeEnum,
                resourceType,
                expression,
                feedOptions,
                resourceLink,
                collection,
                isContinuationExpected,
                correlatedActivityId);
        }

        public bool IsDone
        {
            get { return this.innerExecutionContext.IsDone; }
        }

        public void Dispose()
        {
            this.innerExecutionContext.Dispose();
        }

        public async Task<DocumentFeedResponse<CosmosElement>> ExecuteNextFeedResponseAsync(CancellationToken token)
        {
            if (this.IsDone)
            {
                throw new InvalidOperationException(RMResources.DocumentQueryExecutionContextIsDone);
            }

            Error error = null;

            try
            {
                return await this.innerExecutionContext.ExecuteNextFeedResponseAsync(token);
            }
            catch (DocumentClientException ex)
            {
                if (ex.StatusCode != HttpStatusCode.BadRequest || ex.GetSubStatus() != SubStatusCodes.CrossPartitionQueryNotServable)
                {
                    throw;
                }

                error = ex.Error;
            }

            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo =
                    JsonConvert.DeserializeObject<PartitionedQueryExecutionInfo>(error.AdditionalErrorInfo);

            DefaultDocumentQueryExecutionContext queryExecutionContext =
                (DefaultDocumentQueryExecutionContext)this.innerExecutionContext;

            List<PartitionKeyRange> partitionKeyRanges =
                await
                    queryExecutionContext.GetTargetPartitionKeyRangesAsync(collection.ResourceId,
                        partitionedQueryExecutionInfo.QueryRanges);

            DocumentQueryExecutionContextBase.InitParams constructorParams = new DocumentQueryExecutionContextBase.InitParams(this.client, this.resourceTypeEnum, this.resourceType, this.expression, this.feedOptions, this.resourceLink, false, correlatedActivityId);
            // Devnote this will get replace by the new v3 to v2 logic
            throw new NotSupportedException("v2 query excution context is currently not supported.");

            //return await this.innerExecutionContext.ExecuteNextFeedResponseAsync(token);
        }
    }
}
