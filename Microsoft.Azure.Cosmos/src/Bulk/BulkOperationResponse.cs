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
    public class BulkOperationResponse<TContext> : IDisposable
    {
        private readonly ResponseMessage responseMessage;
        private readonly ContainerInternal container;

        internal BulkOperationResponse(TransactionalBatchOperationResult transactionalBatchOperationResult, 
                                       TContext operationContext,
                                       ContainerInternal container,
                                       Exception ex)
        {
            this.responseMessage = transactionalBatchOperationResult.ToResponseMessage();
            this.Exception = ex;
            this.OperationContext = operationContext;
            this.container = container;
        }

        internal HttpStatusCode StatusCode => this.responseMessage.StatusCode;
        internal Stream ResourceStream => this.responseMessage.Content;
        internal TContext OperationContext { get; }
        internal Exception Exception { get; }
        internal bool IsSuccessStatusCode => this.StatusCode.IsSuccess();

        /// <inheritdoc/>
        public void Dispose()
        {
            this.responseMessage.Dispose();
        }

        internal T GetResource<T>()
        {
            this.responseMessage.EnsureSuccessStatusCode();
            return this.container.ClientContext.SerializerCore.FromStream<T>(this.responseMessage.Content);
        }

        internal Stream GetResource()
        {
            return this.ResourceStream;
        }
    }
}
