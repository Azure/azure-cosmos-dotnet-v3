//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// Handler which selects the pipeline for the requested resource operation
    /// </summary>
    internal class RouterHandler : RequestHandler
    {
        private readonly RequestHandler documentFeedHandler;
        private readonly RequestHandler pointOperationHandler;

        public RouterHandler(
            RequestHandler documentFeedHandler,
            RequestHandler pointOperationHandler)
        {
            this.documentFeedHandler = documentFeedHandler ?? throw new ArgumentNullException(nameof(documentFeedHandler));
            this.pointOperationHandler = pointOperationHandler ?? throw new ArgumentNullException(nameof(pointOperationHandler));
        }

        public override Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestHandler targetHandler = request.IsPartitionKeyRangeHandlerRequired ? this.documentFeedHandler : this.pointOperationHandler;
            return targetHandler.SendAsync(request, cancellationToken);
        }
    }
}