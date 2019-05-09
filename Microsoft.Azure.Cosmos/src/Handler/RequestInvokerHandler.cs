//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// HttpMessageHandler can only be invoked by derived classed or internal classes inside http assembly
    /// </summary>
    internal class RequestInvokerHandler : CosmosRequestHandler
    {
        private readonly CosmosClient client;

        public RequestInvokerHandler(CosmosClient client)
        {
            this.client = client;
        }

        public override Task<CosmosResponseMessage> SendAsync(
            CosmosRequestMessage request, 
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            RequestOptions promotedRequestOptions = request.RequestOptions;
            if (promotedRequestOptions != null)
            {
                // Fill request options
                promotedRequestOptions.FillRequestOptions(request);

                // Validate the request consistency compatibility with account consistency
                // Type based access context for requested consistency preferred for performance
                Cosmos.ConsistencyLevel? consistencyLevel = null;
                if (promotedRequestOptions is ItemRequestOptions)
                {
                    consistencyLevel = (promotedRequestOptions as ItemRequestOptions).ConsistencyLevel;
                }
                else if (promotedRequestOptions is QueryRequestOptions)
                {
                    consistencyLevel = (promotedRequestOptions as QueryRequestOptions).ConsistencyLevel;
                }
                else if (promotedRequestOptions is StoredProcedureRequestOptions)
                {
                    consistencyLevel = (promotedRequestOptions as StoredProcedureRequestOptions).ConsistencyLevel;
                }

                if (consistencyLevel.HasValue)
                {
                    if (!ValidationHelpers.ValidateConsistencyLevel(this.client.AccountConsistencyLevel, consistencyLevel.Value))
                    {
                        throw new ArgumentException(string.Format(
                                CultureInfo.CurrentUICulture,
                                RMResources.InvalidConsistencyLevel,
                                consistencyLevel.Value.ToString(),
                                this.client.AccountConsistencyLevel));
                    }
                }
            }

            return this.client.DocumentClient.EnsureValidClientAsync()
                .ContinueWith(task => request.AssertPartitioningDetailsAsync(client, cancellationToken))
                .ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        throw task.Exception;
                    }

                    this.FillMultiMasterContext(request);
                    return base.SendAsync(request, cancellationToken);
                })
                .Unwrap();
        }

        private void FillMultiMasterContext(CosmosRequestMessage request)
        {
            if (this.client.DocumentClient.UseMultipleWriteLocations)
            {
                request.Headers.Set(HttpConstants.HttpHeaders.AllowTentativeWrites, bool.TrueString);
            }
        }
    }
}
