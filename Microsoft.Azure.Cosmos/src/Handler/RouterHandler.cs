//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Handler which selects the piepline for the requested resource operation
    /// </summary>
    internal class RouterHandler : CosmosRequestHandler
    {
        private readonly CosmosRequestHandler doucumentFeedHandler;
        private readonly CosmosRequestHandler pointOperationHandler;

        public RouterHandler(
            CosmosRequestHandler doucumentFeedHandler, 
            CosmosRequestHandler pointOperationHandler)
        {
            if (doucumentFeedHandler == null)
            {
                throw new ArgumentNullException(nameof(doucumentFeedHandler));
            }

            if (pointOperationHandler == null)
            {
                throw new ArgumentNullException(nameof(pointOperationHandler));
            }

            this.doucumentFeedHandler = doucumentFeedHandler;
            this.pointOperationHandler = pointOperationHandler;
        }

        public override Task<CosmosResponseMessage> SendAsync(
            CosmosRequestMessage request, 
            CancellationToken cancellationToken)
        {
            CosmosRequestHandler targetHandler = null;
            if (request.OperationType == OperationType.ReadFeed && request.ResourceType == ResourceType.Document)
            {
                targetHandler = doucumentFeedHandler;
            }
            else
            {
                targetHandler = pointOperationHandler;
            }

            return targetHandler.SendAsync(request, cancellationToken);
        }
    }
}