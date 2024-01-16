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
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Parallel hedging availability strategy. Once threshold time is reached, 
    /// the SDK will send out an additional request to a remote region in parallel
    /// if the first parallel request or the original has not returned after the step time, 
    /// additional parallel requests will be sent out there is a response or all regions are exausted.
    /// </summary>
#if PREVIEW
    public 
#else
    internal
#endif
    class ParallelHedgingAvailabilityStrategy : AvailabilityStrategy
    {
        /// <summary>
        /// When the SDK decided to activate the availability strategy.
        /// </summary>
        public TimeSpan Threshold { get; private set; }

        /// <summary>
        /// When the SDK will send out additional availability requests after the first one
        /// </summary>
        public TimeSpan Step { get; private set; }

        /// <summary>
        /// Constustor for parallel hedging availability strategy
        /// </summary>
        /// <param name="threshold"></param>
        /// <param name="step"></param>
        public ParallelHedgingAvailabilityStrategy(TimeSpan threshold, TimeSpan? step)
        {
            this.Threshold = threshold;
            this.Step = step ?? TimeSpan.MaxValue;
        }

        internal override bool Enabled()
        {
            return true;
        }

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
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender,
            CosmosClient client,
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            if (!this.ShouldHedge(request))
            {
                return await sender(request, cancellationToken);
            }

            using (CancellationTokenSource cancellationTokenSource = new ())
            {
                CancellationToken parallelRequestCancellationToken = cancellationTokenSource.Token;

                List<RequestMessage> requestMessages = new List<RequestMessage> { request };
                IReadOnlyCollection<string> availableRegions = client.DocumentClient.GlobalEndpointManager.GetAvailableReadEndpointsByLocation().Keys;

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
                    sender, 
                    requestMessages, 
                    cancellationToken, 
                    parallelRequestCancellationToken);
                Task<ResponseMessage> response = await Task.WhenAny(tasks);

                cancellationTokenSource.Cancel();
                return await response;
            }
        }

        private List<Task<ResponseMessage>> RequestTaskBuilder(
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender,
            List<RequestMessage> requests,
            CancellationToken cancellationToken,
            CancellationToken parallelRequestCancellationToken)
        {
            List<Task<ResponseMessage>> tasks = new List<Task<ResponseMessage>>();
            for (int i = 0; i < requests.Count; i++)
            {
                if (i == 0)
                {
                    tasks.Add(sender(requests[i], cancellationToken));
                }
                else
                {
                    TimeSpan delay = this.Threshold + TimeSpan.FromMilliseconds((i - 1) * this.Step.Milliseconds);
                    tasks.Add(this.SendWithDelayAsync(sender, delay, requests[i], cancellationToken, parallelRequestCancellationToken));
                }
            }

            return tasks;
        }

        private async Task<ResponseMessage> SendWithDelayAsync(
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender,
            TimeSpan delay,
            RequestMessage request,
            CancellationToken cancellationToken,
            CancellationToken parallelRequestCancellationToken)
        {
            await Task.Delay(delay, parallelRequestCancellationToken);
            return await sender(request, cancellationToken);
        }
    }
}
