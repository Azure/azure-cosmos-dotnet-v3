//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
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
                IReadOnlyCollection<string> availableRegions = client.DocumentClient.GlobalEndpointManager.GetAvailableReadEndpointsByLocation().Keys;
                int i = 0;
                List<Task<(bool, ResponseMessage)>> requestTasks = new List<Task<(bool, ResponseMessage)>>(availableRegions.Count);

                Task<(bool, ResponseMessage)> primaryRequest = this.RequestSenderAndResultCheckAsync(
                    sender,
                    request,
                    cancellationToken,
                    cancellationTokenSource);

                requestTasks.Add(primaryRequest);

                Task<Task<(bool, ResponseMessage)>> allRequests;

                ResponseMessage responseMessage;
                
                //available regions or hedge regions?
                //Send out hedged requests
                foreach (string region in availableRegions)
                {
                    //Skip the first region as it is the primary request
                    if (i == 0)
                    {
                        i++;
                        continue;
                    }

                    Task<(bool, ResponseMessage)> requestTask = this.CloneAndSendAsync(
                        sender,
                        request,
                        i,
                        availableRegions,
                        cancellationToken,
                        cancellationTokenSource);
                    requestTasks.Add(requestTask);

                    allRequests = Task.WhenAny(requestTasks);

                    //After firing off hedged request, check to see if any previos requests have completed before starting the next one
                    if (allRequests.IsCompleted)
                    {
                        Task<(bool, ResponseMessage)> completedTask = await allRequests;
                        (bool isNonTransient, responseMessage) = await completedTask;

                        if (isNonTransient)
                        {
                            cancellationTokenSource.Cancel();
                            ((CosmosTraceDiagnostics)responseMessage.Diagnostics).Value.AddDatum(
                                "Hedge Context",
                                object.ReferenceEquals(primaryRequest, completedTask) ? "Original Request" : "Hedged Request");
                            return responseMessage;
                        }

                        requestTasks.Remove(completedTask);
                    }

                    i++;
                }

                allRequests = Task.WhenAny(requestTasks);
                //Wait for a good response from the hedged requests/primary request
                while (requestTasks.Count > 1)
                {
                    if (allRequests.IsCompleted)
                    {
                        Task<(bool, ResponseMessage)> completedTask = await allRequests;
                        (bool isNonTransient, responseMessage) = await completedTask;

                        if (isNonTransient)
                        {
                            cancellationTokenSource.Cancel();
                            ((CosmosTraceDiagnostics)responseMessage.Diagnostics).Value.AddDatum(
                                "Hedge Context",
                                object.ReferenceEquals(primaryRequest, completedTask) ? "Original Request" : "Hedged Request");
                            return responseMessage;
                        }

                        requestTasks.Remove(completedTask);
                        allRequests = Task.WhenAny(requestTasks);
                    }
                }

                //If all responses are transient, wait for the last one to finish and return no matter what
                (bool _, ResponseMessage response) = await requestTasks[0];
                cancellationTokenSource.Cancel();
                ((CosmosTraceDiagnostics)response.Diagnostics).Value.AddDatum(
                    "Hedge Context",
                    object.ReferenceEquals(primaryRequest, requestTasks[0]) ? "Original Request" : "Hedged Request");
                return response;
            }
        }

        private async Task<(bool, ResponseMessage)> CloneAndSendAsync(
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender,
            RequestMessage request,
            int requestNumber,
            IReadOnlyCollection<string> regions,
            CancellationToken cancellationToken,
            CancellationTokenSource cancellationTokenSource)
        {
            TimeSpan awaitTime = this.Threshold + TimeSpan.FromMilliseconds((requestNumber - 1) * this.ThresholdStep.Milliseconds);
            await Task.Delay(awaitTime, cancellationToken);

            RequestMessage clonedRequest;
            using (clonedRequest = request.Clone(request.Trace.Parent))
            {
                //Set RequestOptions to exclude the region that was already tried
                if (clonedRequest.RequestOptions == null)
                {
                    clonedRequest.RequestOptions = new RequestOptions()
                    {
                        ExcludeRegions = regions
                        .Where(s => s != regions.ElementAt(requestNumber)).ToList()
                    };
                }
                else
                {
                    if (clonedRequest.RequestOptions.ExcludeRegions == null)
                    {
                        clonedRequest.RequestOptions.ExcludeRegions = regions
                            .Where(s => s != regions.ElementAt(requestNumber)).ToList();
                    }
                    else
                    {
                        clonedRequest.RequestOptions.ExcludeRegions
                            .AddRange(regions.Where(s => s != regions.ElementAt(requestNumber)));
                    }
                }

                return await this.RequestSenderAndResultCheckAsync(
                    sender,
                    clonedRequest,
                    cancellationToken,
                    cancellationTokenSource);

            }
        }

        ///// <summary>
        ///// Execute the parallel hedging availability strategy
        ///// </summary>
        ///// <param name="sender"></param>
        ///// <param name="client"></param>
        ///// <param name="request"></param>
        ///// <param name="cancellationToken"></param>
        ///// <returns>The response after executing cross region hedging</returns>
        //public override async Task<ResponseMessage> ExecuteAvailablityStrategyAsync(
        //    Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender,
        //    CosmosClient client,
        //    RequestMessage request,
        //    CancellationToken cancellationToken)
        //{
        //    if (!this.ShouldHedge(request))
        //    {
        //        return await sender(request, cancellationToken);
        //    }
            
        //    using (CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        //    {
        //        IReadOnlyCollection<string> availableRegions = client.DocumentClient.GlobalEndpointManager.GetAvailableReadEndpointsByLocation().Keys;

        //        List<CosmosDiagnostics> allDiagnostics = new List<CosmosDiagnostics>();

        //        Task<(bool, ResponseMessage)> primaryRequest = this.RequestSenderAndResultCheckAsync(
        //            sender, 
        //            request, 
        //            cancellationToken, 
        //            cancellationTokenSource);
        //        Task hedgeTimer = Task.Delay(this.Threshold, cancellationToken);

        //        Task canStartHedge = await Task.WhenAny(primaryRequest, hedgeTimer);

        //        if (object.ReferenceEquals(canStartHedge, hedgeTimer))
        //        {
        //            Task<List<ResponseMessage>> hedgedRequests = this.SendWithHedgeAsync(
        //            client,
        //            availableRegions,
        //            1,
        //            request,
        //            sender,
        //            cancellationToken,
        //            cancellationTokenSource);

        //            Task result = await Task.WhenAny(primaryRequest, hedgedRequests);
        //            //Primary request finishes first
        //            if (result == primaryRequest)
        //            {
        //                (bool isNonTransient, ResponseMessage primaryResponse) = await primaryRequest;
        //                //Primary request finishes first and is not transient
        //                if (isNonTransient)
        //                {
        //                    cancellationTokenSource.Cancel();
        //                    ((CosmosTraceDiagnostics)primaryResponse.Diagnostics).Value.AddDatum("Hedge Context", "Original Request");
        //                    return primaryResponse;
        //                }
        //            }

        //            //Hedge request finishes first or primary request is transient
        //            List<ResponseMessage> responses = await hedgedRequests;
                    
        //            //Sucessfull response from hedge request
        //            if (responses.Any() && responses[0] != null)
        //            {
        //                cancellationTokenSource.Cancel();
        //                ((CosmosTraceDiagnostics)responses[0].Diagnostics).Value.AddDatum("Hedge Context", "Hedged Request");
        //                return responses[0];
        //            }
        //            else
        //            {
        //                //No successful response from hedge request, return the response from the primary request
        //                cancellationTokenSource.Cancel();
        //                (_, ResponseMessage primaryResponse) = await primaryRequest;
        //                ((CosmosTraceDiagnostics)primaryResponse.Diagnostics).Value.AddDatum("Hedge Context", "Original Request");
        //                return primaryResponse;
        //            }
        //        }
                
        //        (bool nonTransient, ResponseMessage response) = await primaryRequest;

        //        //Primary request fininshes before hedging threshold, but is transient
        //        if (nonTransient)
        //        {
        //            cancellationTokenSource.Cancel();
        //            return response;
        //        }

        //        //If the response from the primary request is transient, we can should try to hedge the request to a different region
        //        List<ResponseMessage> hedgeResponses = await this.SendWithHedgeAsync(
        //            client,
        //            availableRegions,
        //            1,
        //            request,
        //            sender,
        //            cancellationToken,
        //            cancellationTokenSource);
                
        //        cancellationTokenSource.Cancel();

        //        //If no successful response from hedge request, return the response from the primary request
        //        if (hedgeResponses.Any() && !hedgeResponses[0].IsNull())
        //        {
        //            ((CosmosTraceDiagnostics)hedgeResponses[0].Diagnostics).Value.AddDatum("Hedge Context", "Hedged Request");
        //            return hedgeResponses[0];
        //        }
        //        else
        //        {
        //            ((CosmosTraceDiagnostics)response.Diagnostics).Value.AddDatum("Hedge Context", "Original Request");
        //            return response;
        //        }
        //    }
        //}

        private async Task<List<ResponseMessage>> SendWithHedgeAsync(
            CosmosClient client,
            IEnumerable<string> hedgeRegions,
            int requestNumber,
            RequestMessage originalMessage,
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender,
            CancellationToken cancellationToken,
            CancellationTokenSource cancellationTokenSource)
        {
            if (requestNumber != 1)
            {
                await Task.Delay(this.ThresholdStep, cancellationToken);
            }

            List<ResponseMessage> parallelResponses = new List<ResponseMessage>();
            bool disableDiagnostics = originalMessage.RequestOptions != null && originalMessage.RequestOptions.DisablePointOperationDiagnostics;

            RequestMessage clonedRequest;
            using (clonedRequest = originalMessage.Clone(originalMessage.Trace.Parent))
            {
                //Set RequestOptions to exclude the region that was already tried
                if (clonedRequest.RequestOptions == null)
                {
                    clonedRequest.RequestOptions = new RequestOptions()
                    {
                        ExcludeRegions = hedgeRegions
                        .Where(s => s != hedgeRegions.ElementAt(requestNumber)).ToList()
                    };
                }
                else
                {
                    if (clonedRequest.RequestOptions.ExcludeRegions == null)
                    {
                        clonedRequest.RequestOptions.ExcludeRegions = hedgeRegions
                            .Where(s => s != hedgeRegions.ElementAt(requestNumber)).ToList();
                    }
                    else
                    {
                        clonedRequest.RequestOptions.ExcludeRegions
                            .AddRange(hedgeRegions.Where(s => s != hedgeRegions.ElementAt(requestNumber)));
                    }
                }

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return parallelResponses;
                }
                else
                {
                    if (requestNumber == hedgeRegions.Count() - 1)
                    {
                        //If this is the last region to hedge to, do not hedge further
                        (bool isNonTransient, ResponseMessage response) = await this.RequestSenderAndResultCheckAsync(
                            sender,
                            clonedRequest,
                            cancellationToken,
                            cancellationTokenSource);
                        //If the response is non-transient, add response to the responses list
                        //If the response is transient, the empty responses list should be returned
                        if (isNonTransient && response != null)
                        {
                            parallelResponses.Add(response);
                        }
                    }
                    else
                    {
                        Task<(bool, ResponseMessage)> currentHedge = this.RequestSenderAndResultCheckAsync(
                            sender,
                            clonedRequest,
                            cancellationToken,
                            cancellationTokenSource);

                        Task<List<ResponseMessage>> nextHedge = this.SendWithHedgeAsync(
                            client,
                            hedgeRegions,
                            requestNumber + 1,
                            originalMessage,
                            sender,
                            cancellationToken,
                            cancellationTokenSource);

                        Task result = await Task.WhenAny(currentHedge, nextHedge);
                        if (object.ReferenceEquals(result, currentHedge))
                        {
                            (bool, ResponseMessage) response = await currentHedge;
                            //If the response is non-transient, return the response
                            if (response.Item1)
                            {
                                parallelResponses.Insert(0, response.Item2);
                                return parallelResponses;
                            }
                            else
                            {
                                //If the response is transient,
                                //return results from the next hedge
                                List<ResponseMessage> nextResponses = await nextHedge;
                                if (nextResponses.Any())
                                {
                                    nextResponses.AddRange(parallelResponses);
                                    parallelResponses = nextResponses;
                                    parallelResponses.Add(response.Item2);
                                    return parallelResponses;
                                }

                                return await nextHedge;
                            }
                        }
                        else
                        {
                            List<ResponseMessage> nextResponses = await nextHedge;

                            //If the next responses are not empty, return the next responses
                            if (nextResponses.Any())
                            {
                                return nextResponses;
                            }

                            return parallelResponses;
                        }
                    }
                }
                return parallelResponses;
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
