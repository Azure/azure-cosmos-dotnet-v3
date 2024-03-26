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
    using Microsoft.Azure.Cosmos.Tracing;
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
        /// <returns>The response after executing cross region hedging</returns>
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
            
            using (CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                IReadOnlyCollection<string> availableRegions = client.DocumentClient.GlobalEndpointManager.GetAvailableReadEndpointsByLocation().Keys;

                List<CosmosDiagnostics> allDiagnostics = new List<CosmosDiagnostics>();
                Task<(bool, ResponseMessage)> primaryRequest = this.RequestSenderAndResultCheckAsync(
                    sender, 
                    request, 
                    cancellationToken, 
                    cancellationTokenSource);
                Task<List<ResponseMessage>> hedgedRequests = this.SendWithHedgeAsync(
                    client, 
                    availableRegions, 
                    1, 
                    request, 
                    sender, 
                    cancellationToken, 
                    cancellationTokenSource);

                Task result = await Task.WhenAny(primaryRequest, hedgedRequests);
                if (result == primaryRequest)
                {
                    (bool nonTransient, ResponseMessage response) = await primaryRequest;
                    if (nonTransient)
                    {
                        return response;
                    }
                }

                List<ResponseMessage> responses = await hedgedRequests;
                if (responses.Any())
                {
                    return responses[0];
                }
                else
                {
                    return (await primaryRequest).Item2;
                }
            }
        }

        private async Task<List<ResponseMessage>> SendWithHedgeAsync(
            CosmosClient client,
            IReadOnlyCollection<string> availableRegions,
            int requestNumber,
            RequestMessage originalMessage,
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender,
            CancellationToken cancellationToken,
            CancellationTokenSource cancellationTokenSource)
        {
            await Task.Delay(requestNumber == 1 ? this.Threshold : this.ThresholdStep, cancellationToken);
            List<ResponseMessage> parallelResponses = new List<ResponseMessage>();
            bool disableDiagnostics = originalMessage.RequestOptions != null && originalMessage.RequestOptions.DisablePointOperationDiagnostics;
            ITrace clonedTrace;

            using (clonedTrace = disableDiagnostics
                ? NoOpTrace.Singleton
                : Trace.GetRootTrace(originalMessage.Trace.Name, TraceComponent.Transport, TraceLevel.Info))
            {
                clonedTrace.AddDatum("Client Configuration", client.ClientConfigurationTraceDatum);

                RequestMessage clonedRequest = originalMessage.Clone(clonedTrace);

                if (clonedRequest.RequestOptions == null)
                {
                    clonedRequest.RequestOptions = new RequestOptions()
                    {
                        ExcludeRegions = availableRegions
                        .Where(s => s != availableRegions.ElementAt(requestNumber)).ToList()
                    };
                }
                else
                {
                    if (clonedRequest.RequestOptions.ExcludeRegions == null)
                    {
                        clonedRequest.RequestOptions.ExcludeRegions = availableRegions
                            .Where(s => s != availableRegions.ElementAt(requestNumber)).ToList();
                    }
                    else
                    {
                        clonedRequest.RequestOptions.ExcludeRegions
                            .AddRange(availableRegions.Where(s => s != availableRegions.ElementAt(requestNumber)));
                    }
                }

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return parallelResponses;
                }
                else
                {
                    if (requestNumber == availableRegions.Count - 1)
                    {
                        (bool, ResponseMessage) finalResult = await this.RequestSenderAndResultCheckAsync(sender, clonedRequest, cancellationToken, cancellationTokenSource);
                        parallelResponses.Add(finalResult.Item2);
                    }
                    else
                    {
                        Task<(bool, ResponseMessage)> currentHedge = this.RequestSenderAndResultCheckAsync(sender, clonedRequest, cancellationToken, cancellationTokenSource);
                        Task<List<ResponseMessage>> nextHedge = this.SendWithHedgeAsync(client, availableRegions, requestNumber + 1, originalMessage, sender, cancellationToken, cancellationTokenSource);

                        Task result = await Task.WhenAny(currentHedge, nextHedge);
                        if (result == currentHedge)
                        {
                            (bool, ResponseMessage) response = await currentHedge;
                            if (response.Item1)
                            {
                                parallelResponses.Insert(0, response.Item2);
                                return parallelResponses;
                            }
                            else
                            {
                                List<ResponseMessage> nextResponses = await nextHedge;
                                if (nextResponses.Any())
                                {
                                    nextResponses.AddRange(parallelResponses);
                                    parallelResponses = nextResponses;
                                    parallelResponses.Add(response.Item2);
                                    return parallelResponses;
                                }

                                parallelResponses.Add(response.Item2);
                                return parallelResponses;
                            }
                        }
                        else
                        {
                            List<ResponseMessage> nextResponses = await nextHedge;

                            if (nextResponses.Any())
                            {
                                nextResponses.AddRange(parallelResponses);
                                parallelResponses = nextResponses; 

                                return parallelResponses;
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
            catch (OperationCanceledException ex)
            {
                CosmosOperationCanceledException cosmosOperationCanceledException = ex as CosmosOperationCanceledException;
                return (false, null);
            }
            catch (Exception)
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
            // after enforcing the consistency model
            //All other errors should be treated as possibly transient errors
            return statusCode == (int)HttpStatusCode.NotFound && subStatusCode == (int)SubStatusCodes.Unknown;
        }
    }
}
