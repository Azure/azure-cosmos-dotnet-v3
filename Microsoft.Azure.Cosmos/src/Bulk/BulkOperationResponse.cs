//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Response of Bulk Request
    /// </summary>
    public class BulkOperationResponse<TContext>
    {
        private readonly ResponseMessage responseMessage;
        private readonly Container container;

        internal BulkOperationResponse(TransactionalBatchOperationResult transactionalBatchOperationResult, 
                                       TContext operationContext,
                                       ContainerInternal container)
        {
            this.responseMessage = transactionalBatchOperationResult.ToResponseMessage();
            this.OperationContext = operationContext;
            this.container = container;
        }

        internal HttpStatusCode StatusCode { get; }
        internal SubStatusCodes SubStatusCode { get; }
        internal Stream ResourceStream => this.responseMessage.Content;
        internal TContext OperationContext { get; }

        internal T GetResource<T>()
        {
            throw new NotImplementedException();
        }

        internal Stream GetResource()
        {
            return this.ResourceStream;
        }
    }
}
