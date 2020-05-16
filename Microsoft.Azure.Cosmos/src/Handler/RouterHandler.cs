//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Handler which selects the piepline for the requested resource operation
    /// </summary>
    internal class RouterHandler : RequestHandler
    {
        private readonly RequestHandler documentFeedHandler;
        private readonly RequestHandler pointOperationHandler;

        public RouterHandler(
            RequestHandler documentFeedHandler,
            RequestHandler pointOperationHandler)
        {
            if (documentFeedHandler == null)
            {
                throw new ArgumentNullException(nameof(documentFeedHandler));
            }

            if (pointOperationHandler == null)
            {
                throw new ArgumentNullException(nameof(pointOperationHandler));
            }

            this.documentFeedHandler = documentFeedHandler;
            this.pointOperationHandler = pointOperationHandler;
        }

        public override async Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestHandler targetHandler = null;
            if (request.IsPartitionKeyRangeHandlerRequired)
            {
                targetHandler = this.documentFeedHandler;
            }
            else
            {
                targetHandler = this.pointOperationHandler;
            }

            using (request.DiagnosticsContext.CreateRequestHandlerScopeScope(targetHandler))
            {
                return await targetHandler.SendAsync(request, cancellationToken);
            }
        }
    }
}