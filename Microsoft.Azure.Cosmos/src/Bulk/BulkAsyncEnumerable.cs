//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// BulkAsyncEnumerable
    /// </summary>
    internal class BulkAsyncEnumerable<TContext> : IAsyncEnumerable<BulkOperationResponse<TContext>>
    {
        private readonly IAsyncEnumerable<BulkItemOperation<TContext>> inputOperations;
        private readonly BulkRequestOptions bulkRequestOptions;
        private readonly ContainerCore container;

        internal BulkAsyncEnumerable(IAsyncEnumerable<BulkItemOperation<TContext>> inputOperations,
                                     BulkRequestOptions requestOptions,
                                     ContainerCore container)
        {
            this.inputOperations = inputOperations;
            this.bulkRequestOptions = requestOptions;
            this.container = container;
        }

        public IAsyncEnumerator<BulkOperationResponse<TContext>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new BulkAsynEnumerator<TContext>(this.inputOperations,
                                                    this.bulkRequestOptions,
                                                    this.container,
                                                    cancellationToken);
        }
    }
}
