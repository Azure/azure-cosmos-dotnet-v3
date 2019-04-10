//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Handler which selects the piepline for the requested resource operation
    /// </summary>
    internal class RouterHandler : CosmosRequestHandler
    {
        private readonly CosmosRequestHandler documentFeedHandler;
        private readonly CosmosRequestHandler pointOperationHandler;

        public RouterHandler(
            CosmosRequestHandler documentFeedHandler, 
            CosmosRequestHandler pointOperationHandler)
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

        public override Task<CosmosResponseMessage> SendAsync(
            CosmosRequestMessage request, 
            CancellationToken cancellationToken)
        {
            CosmosRequestHandler targetHandler = null;
            if (request.IsDocumentFeedOperation)
            {
                targetHandler = documentFeedHandler;
            }
            else
            {
                targetHandler = pointOperationHandler;
            }

            return targetHandler.SendAsync(request, cancellationToken);
        }
    }
}