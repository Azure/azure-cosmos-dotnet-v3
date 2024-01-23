//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
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
    class CrossRegionParallelHedgingAvailabilityStrategy : AvailabilityStrategy
    {
        /// <summary>
        /// Latency threshold which activates the first region hedging 
        /// </summary>
        public TimeSpan Threshold { get; private set; }

        /// <summary>
        /// When the SDK will send out additional hedging requests after the initial hedging request
        /// </summary>
        public TimeSpan ThresholdStep { get; private set; }

        /// <summary>
        /// Constustor for parallel hedging availability strategy
        /// </summary>
        /// <param name="threshold"></param>
        /// <param name="thresholdStep"></param>
        public CrossRegionParallelHedgingAvailabilityStrategy(TimeSpan threshold, TimeSpan? thresholdStep)
        {
            if (threshold <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(threshold));
            }

            if (thresholdStep <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(thresholdStep));
            }

            this.Threshold = threshold;
            this.ThresholdStep = thresholdStep ?? TimeSpan.MaxValue;
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

        /// <summary>
        /// Execute the parallel hedging availability strategy
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="client"></param>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The response after executing the availability strategy</returns>
#if PREVIEW
        public
#else
        internal
#endif 
        override async Task<ResponseMessage> ExecuteAvailablityStrategyAsync(
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
                
                List<Task<Tuple<bool, ResponseMessage>>> inFlightRequests = new List<Task<Tuple<bool, ResponseMessage>>>();
                _ = this.LazyRequestSchedulerAsync(client, request, inFlightRequests, sender, cancellationToken, parallelRequestCancellationToken);
                int resultLocation = -1;
                Tuple<bool, ResponseMessage> result;

                while (inFlightRequests.Count > 0)
                {
                    Task<Tuple<bool, ResponseMessage>> firstTask = await Task.WhenAny(inFlightRequests);
                    result = await firstTask;
                    resultLocation = inFlightRequests.IndexOf(firstTask);

                    if (result.Item1 == true)
                    {
                        cancellationTokenSource.Cancel();
                        break;
                    }
                }

                result = inFlightRequests[resultLocation].Result;
                List<CosmosDiagnostics> allDiagnostics = new List<CosmosDiagnostics>();
                int i = 0;
                foreach (Task<Tuple<bool, ResponseMessage>> task in inFlightRequests)
                {
                    if (i == 0)
                    {
                        ((CosmosTraceDiagnostics)task.Result.Item2.Diagnostics).Value.AddDatum("Additional Request Context", "Non Hedged Request");
                    }
                    else
                    {
                        ((CosmosTraceDiagnostics)task.Result.Item2.Diagnostics).Value.AddDatum("Additional Request Context", "Hedged Request");
                    }

                    if (task.IsCompleted && resultLocation != i)
                    {
                        allDiagnostics.Add(task.Result.Item2.Diagnostics);
                        ((CosmosTraceDiagnostics)result.Item2.Diagnostics).Value.AddChild(((CosmosTraceDiagnostics)task.Result.Item2.Diagnostics).Value);
                    }
                }

                return result.Item2;
            }
        }

        private async Task LazyRequestSchedulerAsync(
            CosmosClient client,
            RequestMessage originalMessage, 
            List<Task<Tuple<bool, ResponseMessage>>> inFlightRequests,
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender,
            CancellationToken cancellationToken,
            CancellationToken parallelRequestCancellationToken)
        {
            inFlightRequests.Add(this.RequestSenderAndResultCheckAsync(sender, originalMessage, cancellationToken));

            IReadOnlyCollection<string> availableRegions = client.DocumentClient.GlobalEndpointManager.GetAvailableReadEndpointsByLocation().Keys;
            int i = 1;
            RequestMessage clonedRequest;

            while (parallelRequestCancellationToken.IsCancellationRequested == false && i < availableRegions.Count)
            {
                TimeSpan delay = this.ThresholdStep + TimeSpan.FromMilliseconds((inFlightRequests.Count - 1) * this.ThresholdStep.Milliseconds);
                await Task.Delay(delay, parallelRequestCancellationToken);

                clonedRequest = originalMessage.Clone();

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

                inFlightRequests.Add(this.RequestSenderAndResultCheckAsync(sender, clonedRequest, cancellationToken));

                i++;
            }
        }

        private async Task<Tuple<bool, ResponseMessage>> RequestSenderAndResultCheckAsync(
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender,
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await sender(request, cancellationToken);
            if (IsNonTransientResult((int)response.StatusCode, (int)response.Headers.SubStatusCode))
            {
                return new Tuple<bool, ResponseMessage>(true, response);
            }

            return new Tuple<bool, ResponseMessage>(true, response);
        }

        private static bool IsNonTransientResult(int statusCode, int subStatusCode)
        {
            //All 1xx, 2xx, and 3xx status codes should be treated as final results
            if (statusCode < (int)HttpStatusCode.BadRequest)
            {
                return true;
            }

            //Status codes that indicate non-transient timeouts
            if (statusCode == (int)HttpStatusCode.BadRequest
                || statusCode == (int)HttpStatusCode.Conflict
                || statusCode == (int)HttpStatusCode.MethodNotAllowed
                || statusCode == (int)HttpStatusCode.PreconditionFailed
                || statusCode == (int)HttpStatusCode.RequestEntityTooLarge
                || statusCode == (int)HttpStatusCode.Unauthorized)
            {
                return true;
            }

            //404 - Not found is a final result as the document was not yet available
            // after enforcing the consistency model
            if (statusCode == (int)HttpStatusCode.NotFound && subStatusCode == (int)SubStatusCodes.Unknown)
            {
                return true;
            }

            //All other errors should be treated as possibly transient errors
            return false;
        }
    }
}
