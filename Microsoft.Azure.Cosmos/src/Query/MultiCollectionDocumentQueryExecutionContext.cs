//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// This is for routing cross partition queries through the old client side partition collections.
    /// Please ignore.
    /// </summary>
    internal sealed class MultiCollectionDocumentQueryExecutionContext : IDocumentQueryExecutionContext
    {
        private readonly List<IDocumentQueryExecutionContext> childQueryExecutionContexts;
        private int currentChildQueryExecutionContextIndex;

        public static async Task<MultiCollectionDocumentQueryExecutionContext> CreateAsync(
            IDocumentQueryClient client,
            ResourceType resourceTypeEnum,
            Type resourceType,
            Expression expression,
            FeedOptions feedOptions,
            IEnumerable<string> documentFeedLinks,
            bool isContinuationExpected,
            CancellationToken token,
            Guid correlatedActivityId)
        {
            if (client == null)
            {
                throw new ArgumentNullException("client");
            }

            if (feedOptions == null)
            {
                throw new ArgumentNullException("feedOptions");
            }

            if (documentFeedLinks == null)
            {
                throw new ArgumentNullException("documentFeedLinks");
            }

            List<IDocumentQueryExecutionContext> childQueryExecutionContexts = new List<IDocumentQueryExecutionContext>();
            foreach (string link in documentFeedLinks)
            {
                childQueryExecutionContexts.Add(await DocumentQueryExecutionContextFactory.CreateDocumentQueryExecutionContextAsync(
                    client,
                    resourceTypeEnum,
                    resourceType,
                    expression,
                    feedOptions,
                    link, 
                    isContinuationExpected,
                    token,
                    correlatedActivityId));
            }

            return new MultiCollectionDocumentQueryExecutionContext(childQueryExecutionContexts);
        }

        private MultiCollectionDocumentQueryExecutionContext(
            List<IDocumentQueryExecutionContext> childQueryExecutionContexts)
        {
            if (childQueryExecutionContexts == null)
            {
                throw new ArgumentNullException("childQueryExecutionContexts");
            }

            this.childQueryExecutionContexts = childQueryExecutionContexts;
        }

        public bool IsDone
        {
            get { return this.currentChildQueryExecutionContextIndex >= this.childQueryExecutionContexts.Count(); }
        }

        public void Dispose()
        {
            foreach (IDocumentQueryExecutionContext childQueryExecutionContext in this.childQueryExecutionContexts)
            {
                childQueryExecutionContext.Dispose();
            }
        }

        public async Task<FeedResponse<dynamic>> ExecuteNextAsync(CancellationToken token)
        {
            if (this.IsDone)
            {
                throw new InvalidOperationException(RMResources.DocumentQueryExecutionContextIsDone);
            }

            FeedResponse<dynamic> response = await this.childQueryExecutionContexts[this.currentChildQueryExecutionContextIndex].ExecuteNextAsync(token);

            if (this.childQueryExecutionContexts[this.currentChildQueryExecutionContextIndex].IsDone)
            {
                ++this.currentChildQueryExecutionContextIndex;
            }

            return response;
        }
    }
}
