//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Linq;
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
        public CrossRegionParallelHedgingAvailabilityStrategy(
            TimeSpan threshold,
            TimeSpan? thresholdStep)
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
        /// <returns>The response after executing cross region hedging</returns>
        public override async Task<ResponseMessage> ExecuteAvailablityStrategyAsync(
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender,
            CosmosClient client,
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            if (!this.ShouldHedge(request))
            {
                return await sender(request, cancellationToken);
            }

            using (CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                // Get effective order of regions to route to (static once populated)
                IReadOnlyCollection<Uri> availableRegions = client.DocumentClient.GlobalEndpointManager.GetApplicableEndpoints(request.RequestOptions.ExcludeRegions, isReadRequest: true);
                List<Task> requestTasks = new List<Task>(availableRegions.Count + 1);

                ResponseMessage responseMessage = null;
                
                //available regions or hedge regions?
                //Send out hedged requests
                for (int requestNumber = 0; requestNumber < availableRegions.Count; requestNumber++)
                {
                    TimeSpan awaitTime = this.Threshold + TimeSpan.FromMilliseconds(requestNumber * this.ThresholdStep.Milliseconds);
                    Task thresholdDelayTask = Task.Delay(awaitTime, cancellationToken);

                    using (RequestMessage clonedRequest = (requestNumber == 0) ? request : request.Clone(request.Trace.Parent))
                    {
                        clonedRequest.RequestOptions ??= new RequestOptions();

                        clonedRequest.RequestOptions.ExcludeRegions = null;
                        clonedRequest.RequestOptions.LocationEndpointToRoute = availableRegions.ElementAt(requestNumber);

                        Task<(bool, ResponseMessage)> regionRequest = this.RequestSenderAndResultCheckAsync(
                            sender,
                            clonedRequest,
                            cancellationToken,
                            cancellationTokenSource);

                        requestTasks.Add(regionRequest);
                        requestTasks.Add(thresholdDelayTask);
                    }

                    Task completedTask = await Task.WhenAny(requestTasks);
                    requestTasks.Remove(completedTask);

                    if (object.ReferenceEquals(completedTask, thresholdDelayTask))
                    {
                        // Still request not completed, continue hedging into next region if-any
                        continue;
                    }

                    (bool isNonTransient, responseMessage) = await (Task<(bool, ResponseMessage)>)completedTask;
                    if (isNonTransient)
                    {
                        cancellationTokenSource.Cancel();
                        ((CosmosTraceDiagnostics)responseMessage.Diagnostics).Value.AddDatum("Hedge Context", responseMessage.Diagnostics.GetContactedRegions());
                        return responseMessage;
                    }
                }

                //Wait for a good response from the hedged requests/primary request
                while (requestTasks.Count > 1)
                {
                    Task completedTask = await Task.WhenAny(requestTasks);
                    requestTasks.Remove(completedTask);

                    (bool isNonTransient, responseMessage) = await (Task<(bool, ResponseMessage)>)completedTask;
                    if (isNonTransient)
                    {
                        cancellationTokenSource.Cancel();
                        ((CosmosTraceDiagnostics)responseMessage.Diagnostics).Value.AddDatum("Hedge Context", responseMessage.Diagnostics.GetContactedRegions());
                        return responseMessage;
                    }
                }

                Debug.Assert(responseMessage != null);
                return responseMessage;
            }
        }

        private async Task<(bool, ResponseMessage)> RequestSenderAndResultCheckAsync(
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender,
            RequestMessage request,
            CancellationToken cancellationToken,
            CancellationTokenSource cancellationTokenSource)
        {
            try
            {
                ResponseMessage response = await sender.Invoke(request, cancellationToken);
                if (IsNonTransientResult((int)response.StatusCode, (int)response.Headers.SubStatusCode))
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        cancellationTokenSource.Cancel();
                    }
                    return (true, response);
                }

                return (false, response);
            }
            catch (OperationCanceledException)
            {
                return (false, null);
            }
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
            //after enforcing the consistency model
            //All other errors should be treated as possibly transient errors
            return statusCode == (int)HttpStatusCode.NotFound && subStatusCode == (int)SubStatusCodes.Unknown;
        }
    }
}
