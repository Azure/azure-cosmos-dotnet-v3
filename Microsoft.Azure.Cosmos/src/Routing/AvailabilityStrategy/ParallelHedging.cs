//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Parallel hedging availability strategy. Once threshold time is reached, 
    /// the SDK will send out an additional request to a remote region in parallel
    /// if the first parallel request or the original has not returned after the step time, 
    /// additional parallel requests will be sent out there is a response or all regions are exausted.
    /// </summary>
    public class ParallelHedging : AvailabilityStrategy
    {
        /// <summary>
        /// When the SDK decided to activate the availability strategy.
        /// </summary>
        private TimeSpan threshold;

        /// <summary>
        /// When the SDK will send out additional availability requests after the first one
        /// </summary>
        private TimeSpan step;

        /// <summary>
        /// Constustor for parallel hedging availability strategy
        /// </summary>
        /// <param name="threshold"></param>
        /// <param name="step"></param>
        public ParallelHedging(TimeSpan threshold, TimeSpan? step)
        {
            this.threshold = threshold;
            this.step = step ?? TimeSpan.MaxValue;
        }

        /// <summary>
        /// Threshold of when to start availability strategy
        /// </summary>
        public TimeSpan Threshold => this.threshold;

        /// <summary>
        /// Step time to wait before sending out additional parallel requests
        /// </summary>
        public TimeSpan Step => this.step;

        internal bool Enabled { get; private set; } = true;

        /// <summary>
        /// This method determines if the request should be sent with a parallel hedging availability strategy.
        /// This availability strategy can only be used if the request is a read-only request on a document request.
        /// </summary>
        /// <param name="request"></param>
        /// <returns>whether the request should be a parallel hedging request.</returns>
        internal bool ShouldHedge(RequestMessage request)
        {
            //Only use availability strategy for document point operations
            if (request.ResourceType != ResourceType.Document)
            {
                return false;
            }

            //check to see if it is a not a read-only request
            if (!OperationTypeExtensions.IsReadOperation(request.OperationType))
            {
                return false;
            }

            return true;
        }

        internal override async Task<ResponseMessage> ExecuteAvailablityStrategyAsync(
            RequestInvokerHandler requestInvokerHandler,
            CosmosClient client,
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            if (!this.ShouldHedge(request))
            {
                return await requestInvokerHandler.BaseSendAsync(request, cancellationToken);
            }

            using (CancellationTokenSource cancellationTokenSource = new ())
            {
                CancellationToken parallelRequestCancellationToken = cancellationTokenSource.Token;

                List<RequestMessage> requestMessages = new List<RequestMessage> { request };
                IReadOnlyCollection<string> availableRegions = client.DocumentClient.GlobalEndpointManager.GetAvailableReadLocations();

                for (int i = 1; i < availableRegions.Count; i++)
                {
                    RequestMessage clonedRequest = request.Clone();

                    if (clonedRequest.RequestOptions == null)
                    {
                        clonedRequest.RequestOptions = new RequestOptions()
                        {
                            ExcludeRegions = availableRegions
                            .Where(s => s != availableRegions.ElementAt(i)).ToList()
                        };
                    }
                    else
                    {
                        if (clonedRequest.RequestOptions.ExcludeRegions == null)
                        {
                            clonedRequest.RequestOptions.ExcludeRegions = availableRegions
                                .Where(s => s != availableRegions.ElementAt(i)).ToList();
                        }
                        else
                        {
                            clonedRequest.RequestOptions.ExcludeRegions
                                .AddRange(availableRegions.Where(s => s != availableRegions.ElementAt(i)));
                        }
                    }

                    requestMessages.Add(clonedRequest);
                }
                List<Task<ResponseMessage>> tasks = this.RequestTaskBuilder(
                    requestInvokerHandler, 
                    requestMessages, 
                    cancellationToken, 
                    parallelRequestCancellationToken);
                Task<ResponseMessage> response = await Task.WhenAny(tasks);

                cancellationTokenSource.Cancel();
                return await response;
            }
        }

        private List<Task<ResponseMessage>> RequestTaskBuilder(
            RequestInvokerHandler requestInvokerHandler,
            List<RequestMessage> requests,
            CancellationToken cancellationToken,
            CancellationToken parallelRequestCancellationToken)
        {
            List<Task<ResponseMessage>> tasks = new List<Task<ResponseMessage>>();
            for (int i = 0; i < requests.Count; i++)
            {
                if (i == 0)
                {
                    tasks.Add(requestInvokerHandler.BaseSendAsync(requests[i], parallelRequestCancellationToken));
                }
                else
                {
                    TimeSpan delay = this.threshold + TimeSpan.FromMilliseconds((i - 1) * this.step.Milliseconds);
                    tasks.Add(requestInvokerHandler.SendWithDelayAsync(delay, requests[i], cancellationToken, parallelRequestCancellationToken));
                }
            }

            return tasks;
        }
    }
}
