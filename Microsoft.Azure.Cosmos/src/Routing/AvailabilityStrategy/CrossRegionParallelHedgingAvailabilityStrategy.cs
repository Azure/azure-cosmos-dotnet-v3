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
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Diagnostics;
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
        private const string HedgeRegions = "Hedge Regions";
        private const string HedgeContext = "Hedge Context";
        private const string HedgeContextOriginalRequest = "Original Request";
        private const string HedgeContextHedgedRequest = "Hedged Request";

        /// <summary>
        /// Latency threshold which activates the first region hedging 
        /// </summary>
        public TimeSpan Threshold { get; private set; }

        /// <summary>
        /// When the SDK will send out additional hedging requests after the initial hedging request
        /// </summary>
        public TimeSpan ThresholdStep { get; private set; }

        /// <summary>
        /// Constructor for parallel hedging availability strategy
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
            this.ThresholdStep = thresholdStep ?? TimeSpan.FromMilliseconds(-1);
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
        public override async Task<ResponseMessage> ExecuteAvailabilityStrategyAsync(
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
                using (CloneableStream clonedBody = (CloneableStream)(request.Content == null
                    ? null//new CloneableStream(new MemoryStream())
                    : await StreamExtension.AsClonableStreamAsync(request.Content)))
                {
                    IReadOnlyCollection<string> hedgeRegions = client.DocumentClient.GlobalEndpointManager
                    .GetApplicableRegions(
                        request.RequestOptions?.ExcludeRegions,
                        OperationTypeExtensions.IsReadOperation(request.OperationType));

                    List<Task> requestTasks = new List<Task>(hedgeRegions.Count + 1);

                    Task<(bool, ResponseMessage)> primaryRequest = null;

                    ResponseMessage responseMessage = null;

                    //Send out hedged requests
                    for (int requestNumber = 0; requestNumber < hedgeRegions.Count; requestNumber++)
                    {
                        TimeSpan awaitTime = requestNumber == 0 ? this.Threshold : this.ThresholdStep;

                        using (CancellationTokenSource timerTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                        {
                            CancellationToken timerToken = timerTokenSource.Token;
                            using (Task hedgeTimer = Task.Delay(awaitTime, timerToken))
                            {
                                if (requestNumber == 0)
                                {
                                    primaryRequest = this.RequestSenderAndResultCheckAsync(
                                        sender,
                                        request,
                                        cancellationToken,
                                        cancellationTokenSource);

                                    requestTasks.Add(primaryRequest);
                                }
                                else
                                {
                                    Task<(bool, ResponseMessage)> requestTask = this.CloneAndSendAsync(
                                    sender: sender,
                                    request: request,
                                    clonedBody: clonedBody,
                                    hedgeRegions: hedgeRegions,
                                    requestNumber: requestNumber,
                                    cancellationToken: cancellationToken,
                                    cancellationTokenSource: cancellationTokenSource);

                                    requestTasks.Add(requestTask);
                                }

                                requestTasks.Add(hedgeTimer);

                                Task completedTask = await Task.WhenAny(requestTasks);
                                requestTasks.Remove(completedTask);

                                if (completedTask == hedgeTimer)
                                {
                                    continue;
                                }

                                timerTokenSource.Cancel();
                                requestTasks.Remove(hedgeTimer);

                                if (completedTask.IsFaulted)
                                {
                                    AggregateException innerExceptions = completedTask.Exception.Flatten();
                                }

                                (bool isNonTransient, responseMessage) = await (Task<(bool, ResponseMessage)>)completedTask;
                                if (isNonTransient)
                                {
                                    cancellationTokenSource.Cancel();
                                    ((CosmosTraceDiagnostics)responseMessage.Diagnostics).Value.AddOrUpdateDatum(
                                        HedgeRegions,
                                        HedgeRegionsToString(responseMessage.Diagnostics.GetContactedRegions()));
                                    ((CosmosTraceDiagnostics)responseMessage.Diagnostics).Value.AddOrUpdateDatum(
                                        HedgeContext,
                                        object.ReferenceEquals(primaryRequest, completedTask)
                                            ? HedgeContextOriginalRequest
                                            : HedgeContextHedgedRequest);
                                    return responseMessage;
                                }
                            }
                        }
                    }

                    //Wait for a good response from the hedged requests/primary request
                    Exception lastException = null;
                    while (requestTasks.Any())
                    {
                        Task completedTask = await Task.WhenAny(requestTasks);
                        requestTasks.Remove(completedTask);
                        if (completedTask.IsFaulted)
                        {
                            AggregateException innerExceptions = completedTask.Exception.Flatten();
                            lastException = innerExceptions.InnerExceptions.FirstOrDefault();
                        }

                        (bool isNonTransient, responseMessage) = await (Task<(bool, ResponseMessage)>)completedTask;
                        if (isNonTransient || requestTasks.Count == 0)
                        {
                            cancellationTokenSource.Cancel();
                            ((CosmosTraceDiagnostics)responseMessage.Diagnostics).Value.AddOrUpdateDatum(
                                HedgeRegions,
                                HedgeRegionsToString(responseMessage.Diagnostics.GetContactedRegions()));
                            ((CosmosTraceDiagnostics)responseMessage.Diagnostics).Value.AddOrUpdateDatum(
                                HedgeContext,
                                object.ReferenceEquals(primaryRequest, completedTask)
                                ? HedgeContextOriginalRequest
                                : HedgeContextHedgedRequest);
                            return responseMessage;
                        }
                    }

                    if (lastException != null)
                    {
                        throw lastException;
                    }

                    Debug.Assert(responseMessage != null);
                    return responseMessage;
                }
            }
        }

        private async Task<(bool, ResponseMessage)> CloneAndSendAsync(
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender,
            RequestMessage request,
            CloneableStream clonedBody,
            IReadOnlyCollection<string> hedgeRegions,
            int requestNumber,
            CancellationToken cancellationToken,
            CancellationTokenSource cancellationTokenSource)
        {
            RequestMessage clonedRequest;
            using (clonedRequest = request.Clone(request.Trace.Parent, clonedBody))
            {
                clonedRequest.RequestOptions ??= new RequestOptions();

                List<string> excludeRegions = new List<string>(hedgeRegions);
                excludeRegions.RemoveAt(requestNumber);
                clonedRequest.RequestOptions.ExcludeRegions = excludeRegions;

                return await this.RequestSenderAndResultCheckAsync(
                    sender,
                    clonedRequest,
                    cancellationToken,
                    cancellationTokenSource);
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
                if (IsFinalResult((int)response.StatusCode, (int)response.Headers.SubStatusCode))
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        cancellationTokenSource.Cancel();
                    }
                    return (true, response);
                }

                return (false, response);
            }
            catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
            {
                return (false, null);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Exception thrown while executing cross region hedging availability strategy: {0}", ex);
                throw ex;
            }
        }

        private static bool IsFinalResult(int statusCode, int subStatusCode)
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

        private static string HedgeRegionsToString(IReadOnlyList<(string, Uri)> hedgeRegions)
        {
            return string.Join(",", hedgeRegions);
        }
    }
}
