//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.ExecutionComponent
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    internal sealed class GroupByDocumentQueryExecutionComponent : DocumentQueryExecutionComponentBase
    {
        private GroupByDocumentQueryExecutionComponent(
            IDocumentQueryExecutionComponent source)
            : base(source)
        {
        }

        public static async Task<GroupByDocumentQueryExecutionComponent> CreateAsync(
            string continuationToken,
            Func<string, Task<IDocumentQueryExecutionComponent>> createSourceCallback)
        {
            return new GroupByDocumentQueryExecutionComponent(
                await createSourceCallback(continuationToken));
        }

        public override bool IsDone
        {
            get
            {
                return this.Source.IsDone;
            }
        }

        public override async Task<QueryResponse> DrainAsync(
            int maxElements, 
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            QueryResponse sourcePage = await base.DrainAsync(maxElements, token);
            if (!sourcePage.IsSuccessStatusCode)
            {
                return sourcePage;
            }

            // For now the only thing this class is doing is making sure the query does not have continuations
            if(sourcePage.Headers.Continuation != null)
            {
                QueryResponse failedQueryResponse = QueryResponse.CreateFailure(
                    sourcePage.QueryHeaders,
                    System.Net.HttpStatusCode.BadRequest,
                    requestMessage: null,
                    errorMessage: "GROUP BY queries can not span multiple continuations.",
                    error: null);
            }

            return sourcePage;
        }
    }
}