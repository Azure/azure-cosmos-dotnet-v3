//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.ExecutionComponent
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal abstract class DocumentQueryExecutionComponentBase : IDocumentQueryExecutionComponent
    {
        protected readonly IDocumentQueryExecutionComponent source;

        protected DocumentQueryExecutionComponentBase(IDocumentQueryExecutionComponent source)
        {
            this.source = source;
        }

        public virtual bool IsDone
        {
            get
            {
                return this.source.IsDone;
            }
        }

        public virtual void Dispose()
        {
            this.source.Dispose();
        }

        public virtual Task<FeedResponse<object>> DrainAsync(int maxElements, CancellationToken token)
        {
            return this.source.DrainAsync(maxElements, token);
        }

        public void Stop()
        {
            this.source.Stop();
        }

        public IReadOnlyDictionary<string, QueryMetrics> GetQueryMetrics()
        {
            return this.source.GetQueryMetrics();
        }
    }
}
